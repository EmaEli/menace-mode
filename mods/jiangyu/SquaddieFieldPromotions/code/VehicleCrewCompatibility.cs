using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SquaddieFieldPromotions;

internal sealed class VehicleCrewCandidate
{
    internal string PilotTemplateId = "";
    internal string PilotName = "";
    internal string VehicleGuid = "";
    internal string VehicleName = "";
    internal string PersonId = "";
    internal string Role = "";
    internal string Status = "";
    internal string VeterancyRank = "";
    internal int Xp;
    internal bool IsActive;
    internal bool IsPilotDeployed;
}

internal sealed class VehicleCrewMutationReply
{
    internal bool Success;
    internal bool Changed;
    internal string Error = "";
}

/// <summary>Optional, versioned bridge to Veterancy's copied crew records and transaction API.</summary>
internal static class VehicleCrewCompatibility
{
    private const string ApiTypeName = "SquaddieProbe.CompatibilityApi";
    private const int RequiredApiVersion = 2;

    internal static bool IsAvailable
    {
        get
        {
            try { return ApiType() != null; }
            catch { return false; }
        }
    }

    internal static IReadOnlyList<VehicleCrewCandidate> ReadCandidates()
    {
        Type api = ApiType();
        MethodInfo method = RequiredMethod(api, "GetVehicleCrewMembers", Type.EmptyTypes);
        if (Invoke(method, null, null) is not IEnumerable values)
            throw new InvalidOperationException("Veterancy returned no vehicle-crew snapshot.");

        var result = new List<VehicleCrewCandidate>();
        foreach (object value in values)
        {
            if (value == null) continue;
            result.Add(new VehicleCrewCandidate
            {
                PilotTemplateId = Text(value, "PilotTemplateId"),
                PilotName = Text(value, "PilotName"),
                VehicleGuid = Text(value, "VehicleGuid"),
                VehicleName = Text(value, "VehicleName"),
                PersonId = Text(value, "PersonId"),
                Role = Text(value, "Role"),
                Status = Text(value, "Status"),
                VeterancyRank = Text(value, "Rank"),
                Xp = Number(value, "Xp"),
                IsActive = Boolean(value, "IsActive"),
                IsPilotDeployed = Boolean(value, "IsPilotDeployed")
            });
        }
        return result;
    }

    internal static VehicleCrewMutationReply Promote(PromotionRecord record) => InvokeMutation(
        "PromoteVehicleCrewMember", record.SourceCrewPilotTemplateId, record.SourceCrewPersonId,
        record.ReplacementCrewPersonId, record.SourceCrewXp);

    internal static VehicleCrewMutationReply Restore(PromotionRecord record) => InvokeMutation(
        "RestorePromotedVehicleCrewMember", record.SourceCrewPilotTemplateId, record.SourceCrewPersonId,
        record.ReplacementCrewPersonId, record.SourceCrewXp);

    internal static VehicleCrewMutationReply Reconcile(PromotionRecord record) => InvokeMutation(
        "ReconcilePromotedVehicleCrewMember", record.SourceCrewPilotTemplateId, record.SourceCrewPersonId,
        record.ReplacementCrewPersonId, record.SourceCrewXp);

    private static VehicleCrewMutationReply InvokeMutation(string methodName, string pilotTemplateId,
        string sourcePersonId, string replacementPersonId, int xp)
    {
        Type api = ApiType();
        MethodInfo method = RequiredMethod(api, methodName,
            new[] { typeof(string), typeof(string), typeof(string), typeof(int) });
        object reply = Invoke(method, null, new object[]
        {
            pilotTemplateId, sourcePersonId, replacementPersonId, xp
        });
        if (reply == null) throw new InvalidOperationException("Veterancy returned no crew-mutation result.");
        return new VehicleCrewMutationReply
        {
            Success = Boolean(reply, "Success"),
            Changed = Boolean(reply, "Changed"),
            Error = Text(reply, "Error")
        };
    }

    private static Type ApiType()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(ApiTypeName, false);
            if (type == null) continue;
            FieldInfo version = type.GetField("VehicleCrewApiVersion", BindingFlags.Public | BindingFlags.Static);
            if (version?.GetRawConstantValue() is not int value || value < RequiredApiVersion)
                throw new NotSupportedException("Loaded Veterancy mod has no compatible vehicle-crew API.");
            return type;
        }
        throw new InvalidOperationException("Squaddie Veterancy vehicle-crew API is not loaded.");
    }

    private static MethodInfo RequiredMethod(Type type, string name, Type[] parameters) =>
        type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null)
        ?? throw new NotSupportedException("Veterancy vehicle-crew API method is missing: " + name);

    private static object Invoke(MethodInfo method, object target, object[] arguments)
    {
        try { return method.Invoke(target, arguments); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            throw new InvalidOperationException(error.InnerException.Message, error.InnerException);
        }
    }

    private static string Text(object target, string property)
    {
        object value = Read(target, property);
        return value as string ?? "";
    }

    private static int Number(object target, string property) => Convert.ToInt32(Read(target, property));
    private static bool Boolean(object target, string property) => Convert.ToBoolean(Read(target, property));

    private static object Read(object target, string property) =>
        target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target)
        ?? (property == "Error" ? "" : throw new NotSupportedException(
            "Veterancy vehicle-crew API property is missing: " + property));
}