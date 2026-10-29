// Intelmeter contributors, 2026-10-17. Shared picker/read eligibility rules. GPL-2.0-only.
using System;
using Gurux.DLMS.Objects;
namespace GXDLMSDirector
{
    internal static class DashboardAccessPolicy
    {
        internal static int ScalerAttribute(GXDLMSObject obj, int attribute) =>
            obj is GXDLMSRegister && attribute == 2 ? 3 :
            obj is GXDLMSDemandRegister && (attribute == 2 || attribute == 3) ? 4 : 0;
        internal static string AttributeError(GXDLMSObject obj, int attribute, Func<GXDLMSObject, int, bool> canRead)
        {
            if (obj is not IGXDLMSBase supported) return "Object type is not supported by the installed DLMS package.";
            if (attribute < 1 || attribute > supported.GetNames().Length) return "Attribute missing";
            if (!canRead(obj, attribute)) return "Access denied";
            if (obj is GXDLMSProfileGeneric && attribute == 2) return "Use Profile Generic view for ranged buffer reads";
            int scaler = ScalerAttribute(obj, attribute);
            return scaler != 0 && !canRead(obj, scaler) ? "Scaler/unit not readable; cannot safely display this value" : null;
        }
    }
}
