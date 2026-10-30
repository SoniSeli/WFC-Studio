using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using WfcStudio.Core;

namespace WfcStudio.Editor
{
    /// <summary>
    /// The window's Tiles section: a searchable tile list with editable weights, and an adjacency view that shows which
    /// tiles may sit on each side of the selected one. Weights are bound to the tile set asset, so edits support Undo,
    /// and edits made anywhere else (the Inspector, Undo) refresh the panel.
    /// </summary>
    internal sealed class TilesPanel
    {
        static readonly Direction[] Directions = { Direction.Up, Direction.Right, Direction.Down, Direction.Left };

        readonly VisualElement _section;
        readonly ToolbarSearchField _search;
        readonly ListView _list;
        readonly Label _listHint;
        readonly VisualElement _adjacency;
        readonly Image _selectedPreview;
        readonly Label _selectedName;
        readonly Label _selectedSockets;
        readonly VisualElement _neighbours;

        TileSet _tileSet;
        SerializedObject _serialized;
        RuleTable _rules;
        List<int> _visible = new List<int>();
        int _tileCount;
        int _selected = -1;

        public TilesPanel(VisualElement section)
        {
            _section = section;
            _search = section.Q<ToolbarSearchField>("tileSearch");
            _list = section.Q<ListView>("tileList");
            _listHint = section.Q<Label>("tileListHint");
            _adjacency = section.Q("adjacency");
            _selectedName = section.Q<Label>("selectedName");
            _selectedSockets = section.Q<Label>("selectedSockets");
            _neighbours = section.Q("neighbours");

            _selectedPreview = new Image { scaleMode = ScaleMode.ScaleToFit };
            _selectedPreview.AddToClassList("preview-large");
            section.Q("selectedPreview").Add(_selectedPreview);

            _list.fixedItemHeight = 24;
            _list.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _list.selectionType = SelectionType.Single;
            _list.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
            _list.makeItem = MakeRow;
            _list.bindItem = BindRow;
            _list.unbindItem = (row, _) => row.Q<FloatField>().Unbind();
            _list.selectedIndicesChanged += OnSelectionChanged;
            _list.itemsChosen += OnItemsChosen;

            _search.RegisterValueChangedCallback(_ => ApplyFilter());
        }

        /// <summary>Raised after the tile set changes, from this panel or anywhere else.</summary>
        public event Action Edited;

        public void SetTileSet(TileSet tileSet)
        {
            _adjacency.Unbind();
            _tileSet = tileSet;
            _serialized = tileSet != null ? new SerializedObject(tileSet) : null;
            _section.EnableInClassList("hidden", tileSet == null);
            _selected = -1;

            if (tileSet == null)
            {
                _visible = new List<int>();
                _list.itemsSource = _visible;
                _list.Rebuild();
                return;
            }

            // Any edit to the asset, wherever it comes from, lands here.
            _adjacency.TrackSerializedObjectValue(_serialized, _ => OnEdited());
            Recompile();
            ApplyFilter();
            ShowAdjacency();
        }

        void OnEdited()
        {
            Recompile();
            if (_tileSet.Tiles.Count != _tileCount)
            {
                if (_selected >= _tileSet.Tiles.Count)
                    _selected = -1;
                ApplyFilter();
            }
            else
            {
                _list.RefreshItems();
            }

            ShowAdjacency();
            Edited?.Invoke();
        }

        void Recompile()
        {
            _rules = _tileSet.Compile().Rules;
            _tileCount = _tileSet.Tiles.Count;
        }

        void ApplyFilter()
        {
            if (_tileSet == null)
                return;

            _visible = TileSetQueries.Filter(_tileSet.Tiles, _search.value);
            _list.itemsSource = _visible;
            _list.Rebuild();

            int index = _visible.IndexOf(_selected);
            if (index >= 0)
                _list.SetSelectionWithoutNotify(new[] { index });
            else
                _list.ClearSelection();

            _listHint.text = _visible.Count == _tileSet.Tiles.Count
                ? $"{WfcStudioWindow.Plural(_visible.Count, "tile")}. Double-click a tile to select its asset."
                : $"Showing {_visible.Count} of {WfcStudioWindow.Plural(_tileSet.Tiles.Count, "tile")}.";
        }

        VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("tile-row");

            var thumb = new Image { name = "thumb", scaleMode = ScaleMode.ScaleToFit };
            thumb.AddToClassList("thumb");
            row.Add(thumb);

            var name = new Label { name = "name" };
            name.AddToClassList("tile-name");
            row.Add(name);

            var share = new Label { name = "share", tooltip = "Share of the tile set's total weight. A rough guide only: the rules decide where a tile can go." };
            share.AddToClassList("tile-share");
            row.Add(share);

            // Delayed, so the list refreshes once the value is committed rather than on every keystroke.
            var weight = new FloatField { name = "weight", isDelayed = true, tooltip = "Relative chance of picking this tile." };
            weight.AddToClassList("tile-weight");
            row.Add(weight);
            return row;
        }

        void BindRow(VisualElement row, int index)
        {
            int tile = _visible[index];
            var entry = _tileSet.Tiles[tile];
            SetThumbnail(row.Q<Image>("thumb"), entry.Tile);
            row.Q<Label>("name").text = entry.DisplayName;
            row.Q<Label>("share").text = $"{TileSetQueries.WeightShare(_tileSet.Tiles, tile) * 100f:0.0}%";

            var weight = _serialized.FindProperty("_tiles").GetArrayElementAtIndex(tile).FindPropertyRelative("_weight");
            row.Q<FloatField>("weight").BindProperty(weight);
        }

        void OnSelectionChanged(IEnumerable<int> indices)
        {
            _selected = -1;
            foreach (int index in indices)
            {
                _selected = _visible[index];
                break;
            }
            ShowAdjacency();
        }

        void OnItemsChosen(IEnumerable<object> items)
        {
            foreach (object item in items)
            {
                var tile = _tileSet.Tiles[(int)item].Tile;
                if (tile != null)
                {
                    Selection.activeObject = tile;
                    EditorGUIUtility.PingObject(tile);
                }
                return;
            }
        }

        /// <summary>Selects a tile in the list and shows its neighbours, clearing the search if it hides that tile.</summary>
        public void ShowTile(int tile)
        {
            if (!_visible.Contains(tile))
            {
                _search.SetValueWithoutNotify(string.Empty);
                ApplyFilter();
            }

            int index = _visible.IndexOf(tile);
            _list.SetSelection(index);
            _list.ScrollToItem(index);
        }

        void ShowAdjacency()
        {
            _neighbours.Clear();
            bool hasSelection = _tileSet != null && _selected >= 0 && _selected < _tileSet.Tiles.Count;
            _adjacency.EnableInClassList("hidden", !hasSelection);
            if (!hasSelection)
                return;

            var entry = _tileSet.Tiles[_selected];
            SetThumbnail(_selectedPreview, entry.Tile);
            _selectedName.text = entry.DisplayName;
            _selectedSockets.text = "Sockets: " + string.Join(" · ", Array.ConvertAll(Directions, d => $"{d} {SocketLabel(entry, d)}"));

            if (_rules == null)
            {
                _neighbours.Add(Hint("Fix the tile set's errors to see which tiles can touch this one."));
                return;
            }

            foreach (var direction in Directions)
            {
                var neighbours = TileSetQueries.Neighbours(_rules, _selected, direction);

                var block = new VisualElement();
                block.AddToClassList("direction");
                var title = new Label($"{direction} · {SocketLabel(entry, direction)} · {WfcStudioWindow.Plural(neighbours.Count, "tile")}");
                title.AddToClassList("direction-title");
                block.Add(title);

                var thumbs = new VisualElement();
                thumbs.AddToClassList("thumbs");
                foreach (int neighbour in neighbours)
                {
                    var thumb = new Image { scaleMode = ScaleMode.ScaleToFit, tooltip = _tileSet.Tiles[neighbour].DisplayName };
                    thumb.AddToClassList("thumb");
                    thumb.AddToClassList("neighbour");
                    SetThumbnail(thumb, _tileSet.Tiles[neighbour].Tile);
                    int target = neighbour;
                    thumb.RegisterCallback<ClickEvent>(_ => ShowTile(target));
                    thumbs.Add(thumb);
                }
                if (neighbours.Count == 0)
                    thumbs.Add(Hint("Nothing can sit here, so this side can only face the edge of the map."));
                block.Add(thumbs);
                _neighbours.Add(block);
            }
        }

        static void SetThumbnail(Image image, TileBase tile)
        {
            image.image = null;
            image.sprite = null;
            if (tile is Tile withSprite && withSprite.sprite != null)
                image.sprite = withSprite.sprite;
            else if (tile != null)
                image.image = AssetPreview.GetMiniThumbnail(tile);
        }

        static string SocketLabel(TileEntry entry, Direction direction)
        {
            string socket = entry.GetSocket(direction);
            return string.IsNullOrEmpty(socket) ? "(none)" : socket;
        }

        static Label Hint(string text)
        {
            var label = new Label(text);
            label.AddToClassList("hint");
            return label;
        }
    }
}
