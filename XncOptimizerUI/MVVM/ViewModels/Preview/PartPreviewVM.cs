using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Preview
{
    /// <summary>
    /// What the part preview draws for the selected part - its size, turn, sheet thickness and
    /// programs - and the rotation buttons, which turn every XNC program of the part and save the file.
    /// </summary>
    public sealed partial class PartPreviewVM : ObservableObject
    {
        private readonly ProjectServices _project;
        private readonly IConfigService _config;
        private readonly ProjectWorkspace _workspace;
        private readonly ProjectCatalogVM _catalog;
        private readonly SelectedPartMachiningVM _machining;
        private readonly PartEditSaver _saver;

        private bool _neverFlipTclOnTurn;

        // Set while Rotation is updated from the model (part selection, rollback),
        // so the change is not mistaken for an operator request to rotate the part.
        private bool _isSyncingRotation;

        public PartPreviewVM(
            ProjectServices project,
            IConfigService config,
            ProjectWorkspace workspace,
            ProjectCatalogVM catalog,
            SelectedPartMachiningVM machining,
            PartEditSaver saver)
        {
            _project = project;
            _config = config;
            _workspace = workspace;
            _catalog = catalog;
            _machining = machining;
            _saver = saver;
            _neverFlipTclOnTurn = config.NeverFlipTclOnTurn;

            _machining.ProgramsRead += Show;
        }

        #region Properties
        /// <summary>
        /// Compatibility switch, persisted in the application options: part rotation keeps every
        /// edge groove's TCL (<c>c</c>) as authored, like GibLab, instead of flipping Right/Left
        /// to keep the groove on its physical side. Off by default.
        /// </summary>
        public bool NeverFlipTclOnTurn
        {
            get => _neverFlipTclOnTurn;
            set
            {
                if (SetProperty(ref _neverFlipTclOnTurn, value))
                {
                    _config.UpdateNeverFlipTclOnTurn(value);
                }
            }
        }
        #endregion

        #region ObservableProperties
        /// <summary>The part drawn; set last on every refresh, once everything else matches it.</summary>
        [ObservableProperty]
        private PartVM? _part;

        /// <summary>The part's XNC programs, for the machining overlay (bores, grooves, mills).</summary>
        [ObservableProperty]
        private IReadOnlyList<XncProgram> _programs = [];

        /// <summary>
        /// Length / width the preview draws the part at: the first applied XNC program's
        /// <c>dx</c> / <c>dy</c> when the part has any program, otherwise the part's own
        /// <c>Length</c> / <c>Width</c>.
        /// </summary>
        [ObservableProperty]
        private double _displayLength;

        [ObservableProperty]
        private double _displayWidth;

        /// <summary>Thickness (mm) of the part's sheet: the depth of the side views; 0 when unknown.</summary>
        [ObservableProperty]
        private double _sheetThickness;

        /// <summary>
        /// First applied XNC program's <c>turn</c> code (0..3), or <c>null</c> when the part has
        /// no program. <see cref="TurnText"/> is the same value formatted as degrees.
        /// </summary>
        [ObservableProperty]
        private int? _turn;

        [ObservableProperty]
        private string _turnText = string.Empty;

        /// <summary>
        /// Orientation the rotation buttons show. Follows the part's first program turn; an
        /// operator change rotates every XNC program of the part to it and saves the file.
        /// </summary>
        [ObservableProperty]
        private PartTurn _rotation;

        partial void OnRotationChanged(PartTurn oldValue, PartTurn newValue)
        {
            if (_isSyncingRotation)
            {
                return;
            }

            if (!RotatePart(newValue))
            {
                SyncRotation(oldValue);
            }
        }

        /// <summary>Whether the rotation buttons are enabled: a part with at least one XNC program is shown.</summary>
        [ObservableProperty]
        private bool _canRotate;
        #endregion

        #region Methods
        /// <summary>
        /// Sets the display dimensions and turn readout from the part's XNC programs: the first
        /// applied program's <c>dx</c> / <c>dy</c> and <c>turn</c> when the part has any program,
        /// otherwise the part's own length / width and no turn.
        /// </summary>
        private void Show(PartVM? part, IReadOnlyList<XncProgram> programs)
        {
            Programs = programs;

            if (part is null)
            {
                DisplayLength = 0;
                DisplayWidth = 0;
                SheetThickness = 0;
                Turn = null;
                TurnText = string.Empty;
                CanRotate = false;
                SyncRotation(PartTurn.Deg0);
                Part = null;
                return;
            }

            var first = programs.Count > 0 ? programs[0] : null;
            var programSized = first is { Dx: > 0, Dy: > 0 };

            DisplayLength = programSized ? first!.Dx : (double)part.Length;
            DisplayWidth = programSized ? first!.Dy : (double)part.Width;
            SheetThickness = Math.Max((double)(_catalog.FindSheet(part.SheetId)?.Thickness ?? 0m), 0);
            Turn = first?.Turn;
            // No XNC program -> no turn code, but the preview still reads 0° (untouched frame).
            TurnText = first is null ? "0°" : $"{first.TurnDegrees}°";
            CanRotate = first is not null;
            SyncRotation(ToPartTurn(first?.Turn ?? 0));
            Part = part;
        }

        /// <summary>
        /// Turns the part's XNC programs to <paramref name="target"/>, saves the file in place
        /// (together with any pending grid edit of the part) and refreshes the preview and the
        /// tables. Returns <c>false</c> when nothing was rotated, so the caller restores the buttons.
        /// </summary>
        private bool RotatePart(PartTurn target)
        {
            var part = Part;

            if (part is null || !_workspace.IsOpen)
            {
                return false;
            }

            var rotated = _saver.SaveWithPendingEdits(
                part,
                () => _project.Editor.RotatePart(part.Id, (int)target, flipEdgeGrooveTcl: !NeverFlipTclOnTurn),
                $"Part \"{part.Name}\" rotated to {(int)target * 90}°");

            if (rotated)
            {
                _machining.Refresh(part);
            }

            return rotated;
        }

        private void SyncRotation(PartTurn value)
        {
            _isSyncingRotation = true;

            try
            {
                Rotation = value;
            }
            finally
            {
                _isSyncingRotation = false;
            }
        }

        private static PartTurn ToPartTurn(int turn) => (PartTurn)(((turn % 4) + 4) % 4);
        #endregion
    }
}
