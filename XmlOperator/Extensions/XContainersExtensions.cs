using System.Xml.Linq;

namespace XncOptimizer.Extensions
{
    public static class XContainersExtensions
    {
        public static XElement? GetProject(this XDocument document) => document.Element("project");
        public static IEnumerable<XElement> GetOperations(this XElement element) => element.Elements("operation");
        public static IEnumerable<XElement> GetGoods(this XElement element) => element.Elements("good");
        public static XElement? GetPart(this XElement element) => element.Element("part");
        public static IEnumerable<XElement> GetParts(this XElement element) => element.Elements("part");
        public static IEnumerable<XElement> GetParts(this IEnumerable<XElement> element) => element.Elements("part");
        public static XAttribute? GetProgram(this XElement element) => element.Attribute("program");
        public static string? GetProgramValue(this XElement element) => element.Attribute("program")!.Value;
        public static XAttribute? GetTypeId(this XElement element) => element.Attribute("typeId");
        public static string? GetTypeIdValue(this XElement element) => element.Attribute("typeId")!.Value;
        public static XAttribute? GetName(this XElement element) => element.Attribute("name");
        public static string? GetNameValue(this XElement element) => element.Attribute("name")!.Value;
        public static void SetNameValue(this XElement element, string value) => element.Attribute("name")!.Value = value;
        public static XAttribute? GetGroupCode(this XElement element) => element.Attribute("groupCode");
        public static string? GetGroupCodeValue(this XElement element) => element.Attribute("groupCode")!.Value;
        public static XAttribute? GetCode(this XElement element) => element.Attribute("code");
        public static string? GetCodeValue(this XElement element) => element.Attribute("code")!.Value;
        public static void SetCodeValue(this XElement element, string value) => element.Attribute("code")!.Value = value;
        public static XAttribute? GetId(this XElement element) => element.Attribute("id");
        public static string? GetIdValue(this XElement element) => element.Attribute("id")!.Value;
        public static void SetIdValue(this XElement element, string value) => element.Attribute("id")!.Value = value;
        public static XAttribute? GetTypeName(this XElement element) => element.Attribute("typeName");
        public static string? GetTypeNameValue(this XElement element) => element.Attribute("typeName")!.Value;
        public static void SetTypeNameValue(this XElement element, string value) => element.Attribute("typeName")!.Value = value;

        public static XAttribute? GetElbMat(this XElement element) => element.Attribute("elbMat");
        public static string? GetElbMatValue(this XElement element) => element.Attribute("elbMat")!.Value;

        public static XAttribute? GetEllMat(this XElement element) => element.Attribute("ellMat");
        public static string? GetEllMatValue(this XElement element) => element.Attribute("ellMat")!.Value;

        public static XAttribute? GetElrMat(this XElement element) => element.Attribute("elrMat");
        public static string? GetElrMatValue(this XElement element) => element.Attribute("elrMat")!.Value;

        public static XAttribute? GetEltMat(this XElement element) => element.Attribute("eltMat");
        public static string? GetEltMatValue(this XElement element) => element.Attribute("eltMat")!.Value; 
    }
}
