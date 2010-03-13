//
// AlbumListView.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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

using Hyena.Data;
using Hyena.Data.Gui;

using Banshee.Collection;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.Gui;

namespace Banshee.Collection.Gui
{
    public class AlbumListView : TrackFilterListView<AlbumInfo>
    {
        private ColumnCellAlbum renderer;

        public AlbumListView () : base ()
        {
            if (!String.IsNullOrEmpty (Environment.GetEnvironmentVariable ("BANSHEE_DISABLE_GRID"))) {
                column_controller.Add (new Column ("Album", renderer = new ColumnCellAlbum (), 1.0));
                ColumnController = column_controller;
            } else {
                var layout = new DataViewLayoutGrid () {
                    ChildAllocator = () => new DataViewChildAlbum (),
                    View = this
                };

                layout.ChildCountChanged += (o, e) => {
                    var artwork_manager = ServiceManager.Get<ArtworkManager> ();
                    if (artwork_manager != null && e.Value > 0) {
                        int size = (int)((DataViewChildAlbum)layout[0]).ImageSize;
                        artwork_manager.ChangeCacheSize (size, e.Value);
                    }
                };

                ViewLayout = layout;
            }

            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent, PlayerEvent.TrackInfoUpdated);
        }

        protected override Gdk.Size OnMeasureChild ()
        {
            return ViewLayout != null
                ? base.OnMeasureChild ()
                : new Gdk.Size (0, renderer.ComputeRowHeight (this));
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            // TODO: a) Figure out if the track that changed is actually in view
            //       b) xfade the artwork if it is, that'd be slick
            QueueDraw ();
        }
    }
}
