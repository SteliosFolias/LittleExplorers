using OpenCvSharp;
using SkiaSharp;

namespace LittleExplorers {
    public partial class MainPage : ContentPage {
        private static readonly List<string> Colors = new() { "κόκκινο", "πράσινο", "μπλε", "κίτρινο", "πορτοκαλί", "μωβ", "ροζ" };
        private static readonly List<string> Shapes = new() { "κύκλο", "τετράγωνο", "ορθογώνιο", "τρίγωνο" };

        private static readonly Dictionary<string, List<(Scalar lower, Scalar upper)>> HsvColorRanges = new() {
            ["κόκκινο"] = new() {
        (new Scalar(0, 100, 100), new Scalar(10, 255, 255)),
        (new Scalar(160, 100, 100), new Scalar(179, 255, 255)) // κόκκινο έχει 2 ranges
    },
            ["πράσινο"] = new() { (new Scalar(35, 100, 100), new Scalar(85, 255, 255)) },
            ["μπλε"] = new() { (new Scalar(100, 100, 100), new Scalar(130, 255, 255)) },
            ["κίτρινο"] = new() { (new Scalar(20, 100, 100), new Scalar(30, 255, 255)) },
            ["πορτοκαλί"] = new() { (new Scalar(10, 100, 100), new Scalar(20, 255, 255)) },
            ["μωβ"] = new() { (new Scalar(130, 100, 100), new Scalar(160, 255, 255)) },
            ["ροζ"] = new() { (new Scalar(160, 50, 150), new Scalar(170, 255, 255)) }
        };

        private string currentTarget = "";
        private bool targetIsColor = true;

        public MainPage() {
            InitializeComponent();
        }

        private async void OnPlayClicked(object sender, EventArgs e) {
            PlayBtn.Text = "Άλλη πρόκληση";
            ChallengeFrame.IsVisible = true;
            ResultLabel.IsVisible = false;
            PhotoImage.IsVisible = false;

            Random rnd = new();
            targetIsColor = rnd.Next(2) == 0;
            //προς το παρον παιζει μονο για χρωματα
            targetIsColor = true;

            if (targetIsColor) {
                currentTarget = Colors[rnd.Next(Colors.Count)];
                ChallengeLabel.Text = $"Βρες κάτι {currentTarget}!";
            }
            else {
                currentTarget = Shapes[rnd.Next(Shapes.Count)];
                ChallengeLabel.Text = $"Βρες ένα {currentTarget}!";
            }
        }
        private async void OnCameraClicked(object sender, EventArgs e) {
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted) {
                await DisplayAlert("Σφάλμα", "Η εφαρμογή χρειάζεται άδεια για κάμερα", "ΟΚ");
                return;
            }

            try {
                var photo = await MediaPicker.CapturePhotoAsync();
                if (photo == null) return;

                using var stream = await photo.OpenReadAsync();
                //var assembly = typeof(MainPage).Assembly;
                //using var stream = assembly.GetManifestResourceStream("LittleExplorers.Resources.Images.test2.jpg");
                if (stream == null) return;
                var skBitmap = SKBitmap.Decode(stream);

                // Εμφάνιση φωτογραφίας
                using var imgStream = new MemoryStream();
                using var skImage = SKImage.FromBitmap(skBitmap);
                skImage.Encode(SKEncodedImageFormat.Jpeg, 90).SaveTo(imgStream);
                PhotoImage.Source = ImageSource.FromStream(() => new MemoryStream(imgStream.ToArray()));
                PhotoImage.IsVisible = true;

                // Μετατροπή SKBitmap -> Mat
                Mat mat;
                using (var ms = new MemoryStream()) {
                    skImage.Encode(SKEncodedImageFormat.Jpeg, 90).SaveTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    mat = Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
                }

                bool found = targetIsColor ? DetectColorAndShape(mat, currentTarget, null)
                                           : DetectColorAndShape(mat, null, currentTarget);

                ResultLabel.IsVisible = true;

                if (found) {
                    ResultLabel.TextColor = Microsoft.Maui.Graphics.Colors.Green;
                    ResultLabel.Text = "🎉 Μπράβο! Το βρήκες! 🎉";
                    await ResultLabel.ScaleTo(1.5, 300);
                    await ResultLabel.ScaleTo(1, 300);
                    await ResultLabel.ScaleTo(1.3, 300);
                    await ResultLabel.ScaleTo(1, 300);
                }
                else {
                    ResultLabel.TextColor = Microsoft.Maui.Graphics.Colors.Red;
                    ResultLabel.Text = "😢 Δοκίμασε ξανά!";
                }
            }
            catch (Exception ex) {
                ResultLabel.TextColor = Microsoft.Maui.Graphics.Colors.Red;
                ResultLabel.Text = $"Σφάλμα: {ex.Message}";
            }
        }
        private bool DetectColorAndShape(Mat mat, string colorName, string shapeName) {
            // 1️⃣ Μετατροπή σε HSV
            Mat hsv = new Mat();
            Cv2.CvtColor(mat, hsv, ColorConversionCodes.BGR2HSV);

            // 2️⃣ Δημιουργία μάσκας για το χρώμα
            Mat mask = new Mat(mat.Size(), MatType.CV_8UC1, Scalar.All(0));
            if (!string.IsNullOrEmpty(colorName) && HsvColorRanges.ContainsKey(colorName)) {
                foreach (var (lower, upper) in HsvColorRanges[colorName]) {
                    Mat tempMask = new();
                    Cv2.InRange(hsv, lower, upper, tempMask);
                    Cv2.BitwiseOr(mask, tempMask, mask);
                }
            }
            else {
                mask = new Mat(mat.Size(), MatType.CV_8UC1, Scalar.All(255));
            }

            // 3️⃣ Βρες contours
            Cv2.FindContours(mask, out OpenCvSharp.Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            foreach (var contour in contours) {
                double area = Cv2.ContourArea(contour);
                if (area < 500) continue; // αγνόησε πολύ μικρά σχήματα

                // Προσέγγιση πολυγώνου
                double peri = Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, 0.02 * peri, true);

                // 🔍 Έλεγχος ανά σχήμα
                if (!string.IsNullOrEmpty(shapeName)) {
                    switch (shapeName) {
                        case "τρίγωνο":
                            if (approx.Length == 3) return true;
                            break;

                        case "τετράγωνο":
                            if (approx.Length == 4) {
                                var rect = Cv2.BoundingRect(approx);
                                double ar = (double)rect.Width / rect.Height;
                                if (ar > 0.9 && ar < 1.1) return true; // σχεδόν ίσες πλευρές
                            }
                            break;

                        case "ορθογώνιο":
                            if (approx.Length == 4) {
                                var rect = Cv2.BoundingRect(approx);
                                double ar = (double)rect.Width / rect.Height;
                                if (ar <= 0.9 || ar >= 1.1) return true; // όχι τετράγωνο
                            }
                            break;

                        case "κύκλο":
                            double perimeter = Cv2.ArcLength(contour, true);
                            if (perimeter == 0) continue;
                            double circularity = 4 * Math.PI * area / (perimeter * perimeter);
                            if (circularity > 0.85) return true; // κοντά στον τέλειο κύκλο
                            break;
                    }
                }
                else {
                    // ✅ Αν ζητείται μόνο χρώμα
                    if (area > 500) return true;
                }
            }

            return false;
        }

    }
}
