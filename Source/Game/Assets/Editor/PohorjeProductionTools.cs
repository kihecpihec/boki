using UnityEditor;
using UnityEngine;

namespace Boki.Editor
{
    public static class PohorjeProductionTools
    {
        internal static readonly MapProductionTools Delivery = new MapProductionTools("Pohorje", "pohorje", new Color(0.14f, 0.20f, 0.24f));

        [MenuItem("Boki/Pohorje Production/Import and Build Preview")]
        public static void ImportAndBuild() => Delivery.ImportAndBuild();

        [MenuItem("Boki/Pohorje Production/Open Preview")]
        public static void OpenPreview() => Delivery.OpenPreview();

        [MenuItem("Boki/Pohorje Production/Import and Validate Delivery")]
        public static void ImportAndValidate() => Delivery.ImportAndValidate();

        [MenuItem("Boki/Pohorje Production/Export Environment Package")]
        public static void ExportPackage() => Delivery.ExportPackage();

        [MenuItem("Boki/Pohorje Production/Import, Validate and Package")]
        public static void BuildDelivery() => Delivery.BuildDelivery();

        [MenuItem("Boki/Pohorje Production/Capture Review Views")]
        public static void Capture() => Delivery.Capture();

        public static void CaptureAndExport() => Delivery.CaptureAndExport();
    }
}
