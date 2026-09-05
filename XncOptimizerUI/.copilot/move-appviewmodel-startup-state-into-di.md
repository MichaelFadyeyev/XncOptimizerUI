# 🎯 Move AppViewModel Startup State into DI

Refactor the startup-only initialization of `AppViewModel` so the composition root in `XncOptimizerUI/App.xaml.cs` supplies `_assembly`, `_labelsToProcess`, and `_selectedLabel` as separate constructor arguments. Preserve the existing behavior: the assembly name remains in the window title, labels are copied into an independent `ObservableCollection<string>`, and the selected label is seeded without invoking `OnSelectedLabelChanged` or writing configuration back during startup.

The existing `IConfigService` remains injected separately because `AppViewModel` still uses it for label mutations and selected-label persistence after construction. Since the built-in DI container cannot distinguish multiple raw `string` registrations by constructor parameter name, register `AppViewModel` with a factory in `App.xaml.cs`; the factory resolves `IConfigService`, derives the three values, and passes them to the explicit constructor parameters. This keeps the values composed in `App.xaml.cs` without introducing a new options type.

**Progress**: 100% [██████████]

**Last Updated**: 2026-08-31 16:20:18

## 📝 Plan Steps
- ✅ **Update `XncOptimizerUI/MVVM/ViewModels/AppViewModel.cs` — replace the static assembly-name field with an instance field supplied through the constructor, add separate constructor parameters for the assembly name, the initial labels collection, and the initial selected label, and assign the generated backing fields directly to preserve notification and persistence behavior.**
- ✅ **Adjust the initial window-title setup in `AppViewModel` — initialize the generated window-title backing field from the injected assembly name in the constructor, while retaining the existing `OnFullPathChanged` formatting for later file selections; remove the `System.Reflection` dependency if it is no longer used.**
- ✅ **Update `XncOptimizerUI/App.xaml.cs` — replace direct `AddSingleton<AppViewModel>()` registration with a singleton factory that resolves `IProjectService`, `IConfigService`, and `IDialogService`, obtains `Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty`, copies `IConfigService.LabelsToProcess` into an `ObservableCollection<string>`, obtains `GetLastLabelToProcessSelected()`, and passes all three startup values to `AppViewModel`.**
- ✅ **Update `XncOptimizerUI.Test/AppViewModelTests.cs` — provide the three initialization arguments in the direct test constructor helper, keeping the existing assertions that labels are copied and startup selection does not call `UpdateLastLabelToProcessSelectedIndex`; add or adapt coverage for the injected assembly name being reflected in the initial window title if needed by the final implementation.**
- ✅ **Review all `AppViewModel` construction references identified in `App.xaml.cs`, `MainWindow.xaml.cs`, and the test project to ensure the new constructor is only created through the DI factory or with the required explicit values, and verify the .NET 9 solution builds and the existing tests pass.**
