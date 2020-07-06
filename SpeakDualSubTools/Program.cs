using CommandLine;
using CommandLine.Text;
using HtmlAgilityPack;
using Pluralize.NET;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SpeakDualSubTools
{
    class Program
    {


        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(Proceed);
            GetPronounce("");
        }

        static string AppId = Guid.NewGuid().ToString("N").Substring(0,10);
        private static void Proceed(Options o)
        {
            var stream1 = new FileStream(o.InputFiles.ElementAt(0), FileMode.Open, FileAccess.Read);
            var stream2 = new FileStream(o.InputFiles.ElementAt(1), FileMode.Open, FileAccess.Read);

            var file1 = new SubtitlesParser.Classes.Parsers.SrtParser().ParseStream(stream1, Encoding.UTF8).Select(x => new { SubtitleItem = x, Type = "c1" });
            var file2 = new SubtitlesParser.Classes.Parsers.SrtParser().ParseStream(stream2, Encoding.UTF8).Select(x => new { SubtitleItem = x, Type = "c2" });

            var txt = file1.Concat(file2).OrderBy(x => x.SubtitleItem.StartTime).ToList();


            var lineItems = string.Join("<br/>", txt.Select(x =>
            {
                var htmlDoc = new HtmlDocument();

                htmlDoc.LoadHtml(string.Join(" ", x.SubtitleItem.Lines));
                var innerText = Regex.Replace(htmlDoc.DocumentNode.InnerText, @"\t|\n|\r", "");

                innerText = string.Join(" ", innerText.Split(" ").Where(x => x.Trim().Length > 0));

                var words = innerText.Split(new string[] { " ", ".", ",", ":", "!", "?" }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.ToLower()).Distinct();

                var pron = x.Type != "c1" ? "" :
                string.Join("\r\n", words
                    .Select(t => GetPronounce(t))
                    .Where(t => !string.IsNullOrWhiteSpace(t.us))
                    .Select(t => {
                        var id = Guid.NewGuid().ToString("N").Substring(0, 10);
                            return @$"<v onclick='p(""{t.usvoice.Substring(45, t.usvoice.Length - 45)}"")'>{t.word.ToLower()} : {t.us}</v>";
                        })
                    );

                return $"<x class='{x.Type}'> {innerText}</x>" + pron;
            }));

            File.WriteAllText(o.OutputFile, CreateBodyContent(lineItems));
        }

        private static string CreateBodyContent(string bodyHtml)
        {
            return @"
<!doctype html>
<html lang='en'>
  <head>
    <!-- Required meta tags -->
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1, shrink-to-fit=no'>

    <!-- Bootstrap CSS -->
    <link rel='stylesheet' href='https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0/css/bootstrap.min.css' integrity='sha384-Gn5384xqQ1aoWXA+058RXPxPg6fy4IWvTNh0E263XmFcJlSAwiGgFAW/dAiS6JXm' crossorigin='anonymous'>

    <title>Speak English</title>

    <style type='text/css'>
        body {
    
        }
        span {
            margin-top: 10px;
        }
        .c1 {
            color: #FF5722;
            font-size:110%;
        }

        .c2 {
            color: #010348;
        }
    </style>

  </head>
  <body>
<audio preload='none' id='playUsVoice'></audio>
    <div class='container'>
    " + bodyHtml + @"
    </div>

    <script src='https://code.jquery.com/jquery-3.2.1.slim.min.js' integrity='sha384-KJ3o2DKtIkvYIK3UENzmM7KCkRr/rE9/Qpg6aAZGJwFDMVNA/GpGFF93hXpG5KkN' crossorigin='anonymous'></script>
    <script src='https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.12.9/umd/popper.min.js' integrity='sha384-ApNbgh9B+Y1QKtv3Rn7W3mgPxhU9K/ScQsAP7hUibX39j7fakFPskvXusvfa0b4Q' crossorigin='anonymous'></script>
    <script src='https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0/js/bootstrap.min.js' integrity='sha384-JZR6Spejh4U02d8jOt6vLEHfe/JQGiRRSQQxSfFWpi1MquVdAyjUar5+76PVCmYl' crossorigin='anonymous'></script>
    <script src='https://ajax.googleapis.com/ajax/libs/jquery/3.4.1/jquery.min.js'></script>
    <script type = 'text/javascript' >

        function p(voiceCode) {
           $('#playUsVoice').attr('src','http://dictionary.cambridge.org/media/english' + voiceCode).trigger('play');
        }

$('v').addClass('btn').addClass('btn-light');


        $(document).ready(function() {
            console.log('ready.');
            if (localStorage.getItem('" + AppId + @"-quote-scroll') != null)
            {
                $(window).scrollTop(localStorage.getItem('" + AppId + @"-quote-scroll'));
            }

            $(window).on('scroll', function() {
                localStorage.setItem('" + AppId + @"-quote-scroll', $(window).scrollTop());
            });
        });
    </script>

</body>
</html>
";
        }
        static IPluralize pluralizer = new Pluralizer();
        private static DictData GetPronounce(string word)
        {
            word = pluralizer.Singularize(word);
            string us = null, usvoice = null;
            try
            {
                string cs = @"URI=file:dict.db";

                using var con = new SQLiteConnection(cs);
                con.Open();

                string stm = "select uk,us,ukvoice,usvoice from english where word='" + word.ToLower() + "' limit 1";
                using var cmd = new SQLiteCommand(stm, con);
                using SQLiteDataReader rdr = cmd.ExecuteReader();


                while (rdr.Read())
                {
                    us = rdr.GetString(1);
                    usvoice = rdr.GetString(3);
                }

            }
            catch (Exception ex) { }

            return new DictData(us, usvoice, word);
        }
    }

    public class Options
    {
        [Option('r', "read", Required = true, HelpText = "Input files to be processed.")]
        public IEnumerable<string> InputFiles { get; set; }

        [Option('o', "output", Required = true, HelpText = "Location to save the file.")]
        public string OutputFile { get; set; }
    }

    internal struct DictData
    {
        public string us;
        public string usvoice;
        public string word;


        public DictData(string us, string usvoice, string word) 
        {
            this.us = us;
            this.usvoice = usvoice;
            this.word = word;
        }

    }
}
