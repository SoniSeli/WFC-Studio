using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using WfcStudio.Core;

namespace WfcStudio.Editor
{
    /// <summary>
    /// Runs a <see cref="WfcGenerator"/> outside Play mode. An <see cref="EditorApplication.update"/> callback advances
    /// the solver at the generator's own step rate and frame budget, so the editor shows the same live generation as
    /// Play mode. Each action that changes the map is one Undo step. In Play mode the calls go straight to the generator,
    /// whose own Update does the stepping.
    /// </summary>
    internal sealed class EditModeGeneration : IDisposable
    {
        internal const string GenerateUndoName = "Generate Map";
        internal const string StepUndoName = "Step Map";
        internal const string ClearUndoName = "Clear Map";

        // Longest frame counted, so an editor hitch (a recompile, a modal dialog) doesn't release a burst of steps.
        const double MaxDeltaSeconds = 0.1;

        readonly WfcGenerator _generator;
        bool _ticking;
        double _lastTime;

        public EditModeGeneration(WfcGenerator generator)
        {
            _generator = generator;
            Undo.undoRedoEvent += OnUndoRedo;
        }

        /// <summary>Raised after this object changes the map, so views can refresh.</summary>
        public event Action Changed;

        public WfcGenerator Generator => _generator;

        static bool InPlayMode => EditorApplication.isPlaying;

        /// <summary>Starts a new map that fills in live, one batch of steps per editor frame.</summary>
        public void Generate()
        {
            StopTicking();
            RecordMap(GenerateUndoName);
            if (_generator.Generate() && !InPlayMode)
                StartTicking();
            Changed?.Invoke();
        }

        public SolverStatus GenerateInstantly()
        {
            StopTicking();
            RecordMap(GenerateUndoName);
            var status = _generator.GenerateInstantly();
            Changed?.Invoke();
            return status;
        }

        /// <summary>Runs one step and pauses. Starts a paused map first when none is in progress.</summary>
        public void StepOnce()
        {
            if (!_generator.IsGenerating)
            {
                RecordMap(StepUndoName);
                if (!_generator.Generate())
                {
                    Changed?.Invoke();
                    return;
                }
            }

            StopTicking();
            _generator.Pause();
            _generator.StepOnce();
            Changed?.Invoke();
        }

        public void Pause()
        {
            StopTicking();
            _generator.Pause();
            Changed?.Invoke();
        }

        public void Resume()
        {
            _generator.Resume();
            if (_generator.IsGenerating && !InPlayMode)
                StartTicking();
            Changed?.Invoke();
        }

        /// <summary>Finishes the current map in this call.</summary>
        public void Complete()
        {
            StopTicking();
            _generator.Complete();
            Changed?.Invoke();
        }

        public void Clear()
        {
            StopTicking();
            RecordMap(ClearUndoName);
            _generator.Clear();
            Changed?.Invoke();
        }

        public void Dispose()
        {
            StopTicking();
            Undo.undoRedoEvent -= OnUndoRedo;
        }

        // One editor frame of live generation. Driven by EditorApplication.update; tests call it directly.
        internal void Tick(double deltaSeconds)
        {
            if (_generator == null)
            {
                StopTicking();
                return;
            }

            _generator.Tick((float)Math.Min(deltaSeconds, MaxDeltaSeconds));
            if (!_generator.IsGenerating || _generator.IsPaused)
                StopTicking();

            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        void StartTicking()
        {
            if (_ticking)
                return;

            _ticking = true;
            _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        void StopTicking()
        {
            if (!_ticking)
                return;

            _ticking = false;
            EditorApplication.update -= OnEditorUpdate;
        }

        void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            double delta = now - _lastTime;
            _lastTime = now;

            // Play mode's Update takes over the stepping.
            if (InPlayMode)
            {
                StopTicking();
                return;
            }

            Tick(delta);
        }

        // Snapshots the Tilemap so Undo restores the map as it was before this action, however many frames it runs.
        void RecordMap(string undoName)
        {
            var tilemap = _generator.Tilemap;
            if (InPlayMode || tilemap == null)
                return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RegisterCompleteObjectUndo(tilemap, undoName);
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
        }

        // Undoing one of our map actions restores the Tilemap, so the solver behind it no longer matches: drop it.
        // Other undos (a changed seed, say) leave a running map alone.
        void OnUndoRedo(in UndoRedoInfo info)
        {
            if (info.undoName != GenerateUndoName && info.undoName != StepUndoName && info.undoName != ClearUndoName)
                return;

            StopTicking();
            if (_generator != null)
                _generator.DiscardMap();
            Changed?.Invoke();
        }
    }
}
