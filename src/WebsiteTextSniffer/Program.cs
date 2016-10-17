namespace WebsiteTextSniffer
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Media;
    using System.Net;
    using System.Net.Mail;
    using System.Text;
    using System.Threading;
    using System.Web;

    class WebsiteTextSniffer
    {
        private static readonly string _smsEndpoint = ConfigurationManager.AppSettings["smsEndpoint"];
        private static readonly string _smtpEndpoint = ConfigurationManager.AppSettings["smtpEndpoint"];
        private static readonly string _smtpUsername = ConfigurationManager.AppSettings["smtpUsername"];
        private static readonly string _smtpPassword = ConfigurationManager.AppSettings["smtpPassword"];
        private static readonly string _alertFromAddress = ConfigurationManager.AppSettings["alertSendAddress"];
        private static readonly string _alertToAddress = ConfigurationManager.AppSettings["alertEmailAddress"];
        private static readonly string _alertPhoneNumber = ConfigurationManager.AppSettings["alertPhoneNumber"];
        private static readonly int _maxRunIterations = int.Parse(ConfigurationManager.AppSettings["maxRunIterations"]);
        private static readonly int _loopTimeout = int.Parse(ConfigurationManager.AppSettings["loopPauseTime"]);

        const string _monitorWebsite = "https://store.google.com/config/pixel_phone?sku=_pixel_xl_phone_black_128gb";

        static readonly SoundPlayer _alert =
            new SoundPlayer
            {
                SoundLocation = @".\Alert.wav",
            };

        static void PlayWithPause(SoundPlayer soundPlayer, int pause = default(int))
        {
            soundPlayer.Play();
            Thread.Sleep(
                TimeSpan.FromSeconds(
                    pause == default(int) ? 1 : pause));
        }

        static void SendTextAlert(string mobileNumber, string message, Action<string> onWebResponse)
        {
            string dataString = $"number={mobileNumber}&message={HttpUtility.UrlEncode(message)}";
            byte[] dataBytes = Encoding.UTF8.GetBytes(dataString);

            var request = (HttpWebRequest)WebRequest.Create(_smsEndpoint);
            request.ContentType = "application/x-www-form-urlencoded";
            request.Method = "POST";

            request.BeginGetRequestStream(
                result =>
                {
                    using (var stream = request.EndGetRequestStream(result))
                    {
                        stream.Write(dataBytes, 0, dataBytes.Length);
                    }

                    request.BeginGetResponse(
                        resultString =>
                        {
                            var webResponse = (HttpWebResponse)request.EndGetResponse(resultString);
                            using (var reader = new StreamReader(webResponse.GetResponseStream()))
                            {
                                onWebResponse(reader.ReadToEnd());
                            }
                        }, null);
                }, null);
        }

        static void SendEmailAlert(string emailAddress, string subject, string message, Action<string> onSmtpResponse)
        {
            using (var smtpClient = new SmtpClient(_smtpEndpoint))
            {
                var mail = 
                    new MailMessage
                    {
                        From = new MailAddress(_alertFromAddress),
                        Subject = subject,
                        Body = message,
                    };
                mail.To.Add(emailAddress);

                smtpClient.Port = 587;
                smtpClient.Credentials =
                    new NetworkCredential(_smtpUsername, _smtpPassword);
                smtpClient.EnableSsl = true;

                try
                {
                    smtpClient.Send(mail);
                    onSmtpResponse($"Email notification sent to {emailAddress}");
                }
                catch (Exception ex)
                {
                    onSmtpResponse(ex.ToString());
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine($"Starting to monitor:\r\n{_monitorWebsite}\r\n");
            var loop = true;
            var loopCount = 0;
            _alert.Load();

            while (loop)
            {
                Console.WriteLine($"{DateTime.Now}: {loopCount}");
                var webClient =
                    new WebClient
                    {
                        Proxy = null,
                    };

                try
                {
                    var pageText = webClient.DownloadString(_monitorWebsite);
                    if (pageText.Contains("data-available=\"true\" data-backend-docid=\"_pixel_xl_phone_black_32gb\""))
                    {
                        Console.WriteLine($"{DateTime.Now}: Pixel XL Black 32GB in stock!");
                    }
                    if (pageText.Contains("data-available=\"true\" data-backend-docid=\"_pixel_xl_phone_black_128gb\""))
                    {
                        Console.WriteLine($"{DateTime.Now}: Pixel XL Black 128GB in stock!");
                        SendTextAlert(
                            _alertPhoneNumber,
                            $"Pixel XL Black 128GB in stock! {_monitorWebsite}",
                            Console.WriteLine);
                        SendEmailAlert(
                            _alertToAddress, 
                            "Pixel XL Black 128GB in stock!",
                            $"Hey {Environment.UserName}!\r\nPixel XL Black 128GB in stock at {DateTime.Now}!\r\n{_monitorWebsite}",
                            Console.WriteLine);
                        PlayWithPause(_alert);
                        PlayWithPause(_alert);
                        PlayWithPause(_alert);
                        PlayWithPause(_alert);
                        PlayWithPause(_alert);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now}: {ex}");
                }

                Thread.Sleep(TimeSpan.FromSeconds(_loopTimeout));
                loopCount++;

                loop = loopCount <= _maxRunIterations;
            }
        }
    }
}
