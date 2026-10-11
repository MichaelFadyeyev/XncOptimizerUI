using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Contracts
{
    /// <summary>
    /// In-place edits of the open project. None of them saves; the caller saves with
    /// <see cref="IProjectSession.Save"/>. An edit that is refused or fails leaves the document
    /// unchanged and explains why in its log.
    /// </summary>
    public interface IProjectEditor
    {
        /// <summary>
        /// Writes the part's name, size, texture flag and edge bands back. A rename also renames the
        /// part's XNC operations (keeping their <c>[group code]</c>); band changes update every EL
        /// operation's part list and the joint size. Unchanged when nothing differs.
        /// </summary>
        OperationResult UpdatePart(Part part);

        /// <summary>
        /// Turns the part to the absolute <paramref name="targetTurn"/> (<c>0..3</c> = 0°/90°/180°/270°
        /// clockwise): every XNC operation of the part gets <c>turn = targetTurn</c> and its program is
        /// rotated from its own current turn - bores, grooves and mills recalculated, <c>dx</c>/<c>dy</c>
        /// swapped on odd quarter turns, the origin kept at the part's top-left corner. When
        /// <paramref name="flipEdgeGrooveTcl"/> is <c>true</c> an edge groove's <c>c</c> Right/Left flips
        /// where its plane's travel convention reverses; <c>false</c> keeps <c>c</c> as GibLab does.
        /// Refused when the part has no XNC program or the turn is out of range.
        /// </summary>
        OperationResult RotatePart(int partId, int targetTurn, bool flipEdgeGrooveTcl);

        /// <summary>
        /// Sets <paramref name="attribute"/> of the bore at <paramref name="boreIndex"/> (document order
        /// among bore elements) in the program of XNC operation <paramref name="operationId"/> to
        /// <paramref name="expression"/>, written as typed (trimmed) so an expression such as
        /// <c>dx-32</c> stays parametric; checked by <see cref="Services.Xnc.BoreExpression"/> against
        /// the program's symbols. Setting <c>z</c> drops an <c>m="true"</c> middle pin.
        /// </summary>
        OperationResult UpdateBore(int operationId, int boreIndex, BoreAttribute attribute, string expression);

        /// <summary>
        /// Sets <paramref name="attribute"/> of the <paramref name="variableIndex"/>-th <c>&lt;var&gt;</c>
        /// in the program of XNC operation <paramref name="operationId"/>, checked by
        /// <see cref="Services.Xnc.XncVariableRules"/>. A rename rewrites every whole-identifier reference;
        /// a type change keeps the expr and must be valid for it (a referenced variable cannot become
        /// <c>string</c>/<c>bool</c>); an empty comment removes the attribute.
        /// </summary>
        OperationResult UpdateVariable(int operationId, int variableIndex, XncVariableAttribute attribute, string value);
    }
}
