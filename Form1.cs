using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DisplacementAlphaTools {

    public struct DispInfo {
        // This is used to store info pertaining to a particular displacement. 
        // Its coordinates, and the rows of alpha painting information.
        public int _x;
        public int _y;
        public int _width;
        public int _height;
        public List<string> _rows;
    }

    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
        }

        public static Bitmap ImageData { get; set; }
        public static string VMFData { get; set; }
        // True = image, false = VMF.
        public static bool OpenedFileIsImage;


        // A pow 3 displacement holds 9x9 vertices of color information. We should try to fit the entire image
        // into a value less than or equal to the number of required displacements rounded up.

        // Since displacements share an edge (e.g. (9, 0) will be shared by both the first and second brush!),
        // the required length will be...

        // A 09x09 image = 1x1 displacement(s).
        // A 17x17 image = 2x2 displacement(s).
        // A 25x25 image = 3x3 displacement(s).
        // A 33x33 image = 4x4 displacement(s).
        // A 41x41 image = 5x5 displacement(s).

        // The pattern is simply:
        // if (n == 1) size = 9
        // if (n > 1) size = 9 + 8*(n - 1)

        // 8 * (n+1) + 1

        static int xRequired;
        static int yRequired;
        static int width;
        static int height;


        // sRGB luminance(Y) values
        public const double rY = 0.212655;
        public const double gY = 0.715158;
        public const double bY = 0.072187;

        // Inverse of sRGB "gamma" function. (approx 2.2)
        static double InverseGamma_sRGB(int ic) {
            double c = ic / 255.0;
            if (c <= 0.04045) {
                return c / 12.92;
            }
            else {
                return Math.Pow(((c + 0.055) / (1.055)), 2.4);
            }
        }


        // sRGB "gamma" function (approx 2.2)
        static int Gamma_sRGB(double v) {
            if (v <= 0.0031308) {
                v *= 12.92;
            }
            else {
                v = 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
            }
            return Convert.ToInt32(v * 255 + 0.5);
        }


        // GRAY VALUE (luminance)
        static int CalculatePixelLuminance(Color c) {
            return Gamma_sRGB(
                rY * InverseGamma_sRGB(c.R) +
                gY * InverseGamma_sRGB(c.G) +
                bY * InverseGamma_sRGB(c.B)
            );
        }


        static string SidePlaneArrayToString(int[,] SP, int x, int y) {
            string res = "";
            for (int i = 0; i < 3; i++) {
                res += "(";
                for (int j = 0; j < 3; j++) {
                    int val = SP[i, j];
                    if (j == 0) {
                        val += x;
                    }
                    else if (j == 1) {
                        val -= y;
                    }
                    res += Convert.ToString(val);
                    if (j < 2) {
                        res += " ";
                    }
                }
                res += ")";
                if (i < 2) {
                    res += " ";
                }
            }

            return res;
        }


        static void GetMinCoordForPlane(Match planeCoords, ref int minX, ref int minY) {
            for (int cn = 1; cn < 4; cn++) {
                string[] cnCurr = planeCoords.Groups[cn].ToString().Split(' ');
                int currX = Convert.ToInt32(cnCurr[0]);
                int currY = Convert.ToInt32(cnCurr[1]);
                if (minX == int.MaxValue || Convert.ToInt32(cnCurr[0]) < minX) {
                    minX = currX;
                }
                if (minY == int.MaxValue || Convert.ToInt32(cnCurr[0]) < minY) {
                    minY = currY;
                }
            }
        }


        static Image CreatePreviewImage(Image img) {
            int finalWidth = 8 * xRequired + 1;
            int finalHeight = 8 * yRequired + 1;

            Bitmap imgTemp = new Bitmap(img);
            Bitmap bottomLayer = new Bitmap(finalWidth, finalHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            for (int y = 0; y < imgTemp.Height; y++) {
                for (int x = 0; x < imgTemp.Width; x++) {
                    Color oldPixel = imgTemp.GetPixel(x, y);
                    int lum = CalculatePixelLuminance(oldPixel);
                    Color newPixel = Color.FromArgb(oldPixel.A, lum, lum, lum);
                    imgTemp.SetPixel(x, y, newPixel);
                }
            }

            Bitmap finalImage = new Bitmap(finalWidth, finalHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(finalImage)) {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

                graphics.DrawImage(bottomLayer, 0, 0);
                graphics.DrawImage(imgTemp, 0, 0);
            }

            return finalImage;
        }



        private void loadImageToolStripMenuItem_Click(object sender, EventArgs e) {
            using (OpenFileDialog ofd1 = new OpenFileDialog()) {
                ofd1.Title = "Open Picture";
                ofd1.Filter = "Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp";

                if (ofd1.ShowDialog() == DialogResult.OK) {
                    Image tempImg = Image.FromFile(ofd1.FileName);
                    // These values need to be assigned before the preview image and ImageData can proceed.
                    width = tempImg.Width;
                    height = tempImg.Height;
                    // (n - 9) / 8 + 1
                    xRequired = (width < 9) ? 1 : Convert.ToInt32(Math.Ceiling(Convert.ToDouble(width - 9) / 8.0)) + 1;
                    yRequired = (height < 9) ? 1 : Convert.ToInt32(Math.Ceiling(Convert.ToDouble(height - 9) / 8.0)) + 1;

                    Console.WriteLine($"xRequired: {xRequired}");
                    Console.WriteLine($"yRequired: {yRequired}");

                    PreviewPictureBox.Image = CreatePreviewImage(tempImg);
                    ImageData = (Bitmap)PreviewPictureBox.Image;
                    OpenedFileIsImage = true;
                }
            }
        }


        private void loadVMFToolStripMenuItem_Click(object sender, EventArgs e) {
            using (OpenFileDialog ofd1 = new OpenFileDialog()) {
                ofd1.Title = "Open Valve Map File";
                ofd1.Filter = "Valve Map File (*.vmf)|*.vmf";
                if (ofd1.ShowDialog() == DialogResult.OK) {
                    Console.WriteLine("This doesn't do anything yet, but file was successfully selected for opening!");
                    OpenedFileIsImage = false;

                    VMFData = File.ReadAllText(ofd1.FileName);

                    SaveButton.Text = "Save as image";
                    // Todo: make this affect the save button functioning!

                    // Where the data for each respective displacement will be stored...
                    List<DispInfo> displacements = new List<DispInfo>();

                    // Matching the top side of a brush and the contents of it...
                    Regex sidePattern = new Regex(@"(side\s*{[\w\W]+?""id""\s""1""[\w\W]+?})\s*side");
                    // Matching the alpha arrays of the dispinfo section in a .vmf file...
                    Regex alphaPattern = new Regex(@"alphas\s*\{([a-z0-9""\s]*)\}");
                    // Matching the rows of a given displacement's alpha array...
                    Regex rowContentsPattern = new Regex(@"""row\d""\s""([\d\s]+)""");
                    // Matching the plane coordinates of the side...
                    Regex coordinatesPattern = new Regex(@"""plane""\s""\(([\d\D]+)\)\s\(([\d\D]+)\)\s\(([\d\D]+)\)""");

                    MatchCollection displacementSideMatches = sidePattern.Matches(VMFData);
                    for (int i = 0; i < displacementSideMatches.Count; i++) {
                        // Get the current alpha array in the given side.
                        string dsmStr = displacementSideMatches[i].Groups[1].ToString();
                        string currAlphaArray = alphaPattern.Match(dsmStr).Groups[1].ToString();
                        // Get the contents of the rows in the alpha array as discrete regex matches.
                        MatchCollection currRowContents = rowContentsPattern.Matches(currAlphaArray);

                        List<string> currChunk = new List<string>();
                        // Iterate over them and populate a list with their contents.
                        for (int j = 0; j < currRowContents.Count; j++) {
                            currChunk.Add(currRowContents[j].Groups[1].ToString());
                        }
                        // Get the plane coordinates to extract an X and Y value.
                        Match planeCoords = coordinatesPattern.Match(dsmStr);
                        // The minimum value of the X and Y of these three XYZ coordinates is what's needed. It'll serve as the top left corner.
                        int minX = int.MaxValue;
                        int minY = int.MaxValue;
                        GetMinCoordForPlane(planeCoords, ref minX, ref minY);
                        DispInfo d = new DispInfo {
                            _rows = currChunk,
                            _x = minX,
                            _y = minY,
                        };
                        //Console.WriteLine($"X is {minX}, Y is {minY}");
                        displacements.Add(d);
                    }
                }
            }
        }


        private void SaveButton_Click(object sender, EventArgs e) {

            if (OpenedFileIsImage) {
                string sOutput = "";
                Random rng = new Random();
                int solidPosX = 0;
                int solidPosY = 0;
                int solidID = 1;

                // Get the boilerplate for the beginning:
                sOutput += File.ReadAllText("boilerplate_start.txt") + "\n";
                string boilerplateSolid = File.ReadAllText("boilerplate_solid.txt");

                int bSize = 128;  // Brush size.

                // SP = Side Plane.
                int[,] SP1 = { { 0, -bSize, bSize / 2 }, { 0, 0, bSize / 2 }, { bSize, 0, bSize / 2 } };
                int[,] SP2 = { { 0, 0, 0 }, { 0, -bSize, 0 }, { bSize, -bSize, 0 } };
                int[,] SP3 = { { 0, -bSize, 0 }, { 0, 0, 0 }, { 0, 0, bSize / 2 } };
                int[,] SP4 = { { bSize, 0, 0 }, { bSize, -bSize, 0 }, { bSize, -bSize, bSize / 2 } };
                int[,] SP5 = { { 0, 0, 0 }, { bSize, 0, 0 }, { bSize, 0, bSize / 2 } };
                int[,] SP6 = { { bSize, -bSize, 0 }, { 0, -bSize, 0 }, { 0, -bSize, bSize / 2 } };

                List<string> solids = new List<string>();

                for (int y = 0; y < yRequired; y++) {
                    for (int x = 0; x < xRequired; x++) {
                        string newSolid = boilerplateSolid;
                        newSolid = newSolid
                            .Replace("$ID", Convert.ToString(solidID))
                            .Replace("$COLOR", $"{rng.Next(96, 256)} {rng.Next(96, 256)} {rng.Next(96, 256)}")
                            .Replace("$START_POS", $"{x * bSize} {-bSize * (y + 1)} 0")
                            .Replace("$PLANE_1", SidePlaneArrayToString(SP1, x * bSize, y * bSize))
                            .Replace("$PLANE_2", SidePlaneArrayToString(SP2, x * bSize, y * bSize))
                            .Replace("$PLANE_3", SidePlaneArrayToString(SP3, x * bSize, y * bSize))
                            .Replace("$PLANE_4", SidePlaneArrayToString(SP4, x * bSize, y * bSize))
                            .Replace("$PLANE_5", SidePlaneArrayToString(SP5, x * bSize, y * bSize))
                            .Replace("$PLANE_6", SidePlaneArrayToString(SP6, x * bSize, y * bSize));

                        solidID++;
                        solids.Add(newSolid);
                    }
                }

                for (int n = 0; n < solids.Count; n++) {
                    int col = n % xRequired;
                    int row = n / yRequired;  // What row the given n value would put us on.
                    string alphaPaintData = "";
                    // Iterate over the Y coord values of a displacement brush:
                    for (int y = 0; y < 9; y++) {
                        alphaPaintData += $"\t\t\t\t\t\"row{y}\" \"";
                        // Iterate over the X coord values of a displacement brush:
                        for (int x = 0; x < 9; x++) {
                            int actualX = x + col * 9 - col;
                            // Y should be inverted or the displacement chunks for a given row will be upside down!
                            int actualY = (8 - y) + row * 9 - row;
                            int alphaValue = 0;
                            if (actualX < ImageData.Width && actualY < ImageData.Height) {
                                Color currPixel = ImageData.GetPixel(actualX, actualY);
                                alphaValue = CalculatePixelLuminance(currPixel) * currPixel.A / 255;
                            }
                            alphaPaintData += Convert.ToString(alphaValue) + " ";
                        }
                        alphaPaintData += "\"\n";
                    }
                    solids[n] = solids[n].Replace("$ALPHAS", alphaPaintData);
                    sOutput += solids[n] + "\n";
                }

                // Write ending boilerplate to newContents:
                sOutput += File.ReadAllText("boilerplate_end.txt");

                // Save to a file:
                /*using (StreamWriter sw = new StreamWriter("output.vmf")) {
                    sw.Write(sOutput);
                }*/

                SaveFileDialog sfd1 = new SaveFileDialog();
                sfd1.Filter = "Valve Map File|*.vmf";
                sfd1.Title = "Save as .vmf";
                sfd1.ShowDialog();

                if (sfd1.FileName != "") {
                    using (StreamWriter sw = new StreamWriter(sfd1.FileName)) {
                        sw.Write(sOutput);
                    }
                }
            }
            else {
                Console.WriteLine("OpenedFileIsImage is false! You've loaded a .vmf file instead of an image.");
            }
        }
    }
}
