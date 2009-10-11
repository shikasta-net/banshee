//
// Details.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;

using Mono.Unix;

using Hyena.Json;

namespace InternetArchive
{
    public class Details
    {
        JsonObject details;
        JsonObject metadata, misc, item, review_info;
        JsonArray reviews;

        public static Details LoadOrCreate (string id, string title)
        {
            /*var item = Provider.LoadFromId (id);
            if (item != null)
                return item;*/

            return new Details (id, title);
        }

        private Details (string id, string title)
        {
            Id = id;
            Title = title;
            //Provider.Save ();

            LoadDetails ();
        }

#region Properties stored in database columns

        //[DatabaseColumn (PrimaryKey=true)]
        public long DbId { get; set; }

        //[DatabaseColumn]
        public string Id { get; set; }

        //[DatabaseColumn]
        public string Title { get; private set; }

        //[DatabaseColumn]
        public string JsonDetails { get; set; }

        //[DatabaseColumn]
        public bool IsHidden { get; set; }

#endregion

#region Properties from the JSON object

        public string Description {
            get { return metadata.GetJoined ("description", System.Environment.NewLine); }
        }

        public string Creator {
            get { return metadata.GetJoined ("creator", ", "); }
        }

        public string Publisher {
            get { return metadata.GetJoined ("publisher", ", "); }
        }

        public string Year {
            get { return metadata.GetJoined ("year", ", "); }
        }

        public string Subject {
            get { return metadata.GetJoined ("subject", ", "); }
        }

        public string Source {
            get { return metadata.GetJoined ("source", ", "); }
        }

        public string Taper {
            get { return metadata.GetJoined ("taper", ", "); }
        }

        public string Lineage {
            get { return metadata.GetJoined ("lineage", ", "); }
        }

        public string Transferer {
            get { return metadata.GetJoined ("transferer", ", "); }
        }

        public DateTime DateAdded {
            get { return GetMetadataDate ("addeddate"); }
        }

        public string AddedBy {
            get { return metadata.GetJoined ("adder", ", "); }
        }

        public string Venue {
            get { return metadata.GetJoined ("venue", ", "); }
        }

        public string Coverage {
            get { return metadata.GetJoined ("coverage", ", "); }
        }

        public string ImageUrl {
            get { return misc.Get<string> ("image"); }
        }

        public long DownloadsAllTime {
            get { return (int)item.Get<double> ("downloads"); }
        }

        public long DownloadsLastMonth {
            get { return (int)item.Get<double> ("month"); }
        }

        public long DownloadsLastWeek {
            get { return (int)item.Get<double> ("week"); }
        }

        public DateTime DateCreated {
            get { return GetMetadataDate ("date"); }
        }

        private DateTime GetMetadataDate (string key)
        {
            DateTime ret;
            if (DateTime.TryParse (metadata.GetJoined (key, null), out ret))
                return ret;
            else
                return DateTime.MinValue;
        }

        public IEnumerable<DetailsFile> Files {
            get {
                string location_root = String.Format ("http://{0}{1}", details.Get<string> ("server"), details.Get<string> ("dir"));
                var files = details["files"] as JsonObject;
                foreach (JsonObject file in files.Values) {
                    yield return new DetailsFile (file, location_root);
                }
            }
        }

        public IEnumerable<DetailsReview> Reviews {
            get {
                if (reviews == null) {
                    yield break;
                }

                foreach (JsonObject review in reviews) {
                    yield return new DetailsReview (review);
                }
            }
        }

        public double AvgRating {
            get { return review_info.Get<double> ("avg_rating"); }
        }

        public int NumReviews {
            get { return review_info.Get<int> ("num_reviews"); }
        }

        public string WebpageUrl {
            get { return String.Format ("http://www.archive.org/details/{0}", Id); }
        }

#endregion

        private bool LoadDetails ()
        {
            // First see if we already have the Hyena.JsonObject parsed
            if (details != null) {
                return true;
            }

            // If not, see if we have a copy in the database, and parse that
            /*if (JsonDetails != null) {
                details = new Hyena.Json.Deserializer (JsonDetails).Deserialize () as JsonObject;
                return true;
            }*/


            // Hack to load JSON data from local file instead of from archive.org
            if (Id == "banshee-internet-archive-offline-mode") {
                details = new Hyena.Json.Deserializer (System.IO.File.ReadAllText ("item2.json")).Deserialize () as JsonObject;
            } else {
                // We don't; grab it from archive.org and parse it
                string json_str = GetDetails (Id);

                if (json_str != null) {
                    details = new Hyena.Json.Deserializer (json_str).Deserialize () as JsonObject;
                    JsonDetails = json_str;
                }
            }

            if (details != null) {
                metadata = details.Get<JsonObject> ("metadata");
                misc     = details.Get<JsonObject> ("misc");
                item     = details.Get<JsonObject> ("item");
                var r    = details.Get<JsonObject> ("reviews");
                if (r != null) {
                    reviews = r.Get<JsonArray> ("reviews");
                    review_info = r.Get<JsonObject> ("info");
                }
            }

            return false;
        }

        private static string GetDetails (string id)
        {
            HttpWebResponse response = null;
            string url = String.Format ("http://www.archive.org/details/{0}&output=json", id);

            try {
                Hyena.Log.Debug ("ArchiveSharp Getting Details", url);

                var request = (HttpWebRequest) WebRequest.Create (url);
                request.UserAgent = Search.UserAgent;
                request.Timeout   = Search.TimeoutMs;
                request.KeepAlive = Search.KeepAlive;

                response = (HttpWebResponse) request.GetResponse ();

                if (response.StatusCode != HttpStatusCode.OK) {
                    return null;
                }

                using (Stream stream = response.GetResponseStream ()) {
                    using (StreamReader reader = new StreamReader (stream)) {
                        return reader.ReadToEnd ();
                    }
                }
            } finally {
                if (response != null) {
                    if (response.StatusCode != HttpStatusCode.OK) {
                        Hyena.Log.WarningFormat ("Got status {0} searching {1}", response.StatusCode, url);
                    }
                    response.Close ();
                    response = null;
                }
            }
        }
    }
}
