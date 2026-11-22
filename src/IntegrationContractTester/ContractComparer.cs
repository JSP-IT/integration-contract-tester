using IntegrationContractTester.Models;

namespace IntegrationContractTester;

internal static class ContractComparer
{
    public static IReadOnlyList<Finding> Compare(ApiContract baseline, ApiContract candidate)
    {
        var findings = new List<Finding>();

        foreach (var warning in baseline.Warnings.Concat(candidate.Warnings).Distinct(StringComparer.Ordinal))
        {
            findings.Add(new Finding(FindingSeverity.Warning, "DOCUMENT_WARNING", "document", warning));
        }

        foreach (var (operationKey, oldOperation) in baseline.Operations)
        {
            if (!candidate.Operations.TryGetValue(operationKey, out var newOperation))
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    "OPERATION_REMOVED",
                    oldOperation.DisplayName,
                    "The operation is no longer present."));
                continue;
            }

            var operationName = newOperation.DisplayName;
            CompareParameters(operationName, oldOperation, newOperation, findings);
            CompareRequestBody(operationName, oldOperation, newOperation, findings);
            CompareResponses(operationName, oldOperation, newOperation, findings);
        }

        foreach (var (operationKey, newOperation) in candidate.Operations)
        {
            if (baseline.Operations.ContainsKey(operationKey))
            {
                continue;
            }

            findings.Add(new Finding(
                FindingSeverity.NonBreaking,
                "OPERATION_ADDED",
                newOperation.DisplayName,
                "A new operation was added."));
        }

        return findings
            .OrderBy(finding => finding.Severity)
            .ThenBy(finding => finding.Location, StringComparer.Ordinal)
            .ThenBy(finding => finding.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static void CompareParameters(
        string operationKey,
        OperationContract baseline,
        OperationContract candidate,
        List<Finding> findings)
    {
        foreach (var (parameterKey, oldParameter) in baseline.Parameters)
        {
            if (!candidate.Parameters.TryGetValue(parameterKey, out var newParameter))
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    "PARAMETER_REMOVED",
                    $"{operationKey} parameter {oldParameter.DisplayName}",
                    "A parameter accepted by the previous contract was removed."));
                continue;
            }

            var location = $"{operationKey} parameter {newParameter.DisplayName}";

            if (!oldParameter.Required && newParameter.Required)
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    "PARAMETER_NOW_REQUIRED",
                    location,
                    "The parameter changed from optional to required."));
            }
            else if (oldParameter.Required && !newParameter.Required)
            {
                findings.Add(new Finding(
                    FindingSeverity.NonBreaking,
                    "PARAMETER_NOW_OPTIONAL",
                    location,
                    "The parameter changed from required to optional."));
            }

            if (!string.Equals(oldParameter.Serialization, newParameter.Serialization, StringComparison.Ordinal))
            {
                findings.Add(new Finding(
                    FindingSeverity.Warning,
                    "PARAMETER_SERIALIZATION_CHANGED",
                    location,
                    "Parameter serialization settings changed; compatibility is not inferred."));
            }

            CompareSchemas(oldParameter.Schema, newParameter.Schema, location, SchemaDirection.Request, findings);
        }

        foreach (var (parameterKey, parameter) in candidate.Parameters)
        {
            if (baseline.Parameters.ContainsKey(parameterKey))
            {
                continue;
            }

            findings.Add(new Finding(
                parameter.Required ? FindingSeverity.Breaking : FindingSeverity.NonBreaking,
                parameter.Required ? "REQUIRED_PARAMETER_ADDED" : "OPTIONAL_PARAMETER_ADDED",
                $"{operationKey} parameter {parameter.DisplayName}",
                parameter.Required
                    ? "A new required parameter prevents existing clients from calling the operation unchanged."
                    : "An optional parameter was added."));
        }
    }

    private static void CompareRequestBody(
        string operationKey,
        OperationContract baseline,
        OperationContract candidate,
        List<Finding> findings)
    {
        var location = $"{operationKey} request body";
        if (baseline.RequestBody is null)
        {
            if (candidate.RequestBodyRequired)
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    "REQUIRED_REQUEST_BODY_ADDED",
                    location,
                    "The operation now requires a request body."));
            }
            else if (candidate.RequestBody is not null)
            {
                findings.Add(new Finding(
                    FindingSeverity.NonBreaking,
                    "OPTIONAL_REQUEST_BODY_ADDED",
                    location,
                    "An optional request body was added."));
            }

            return;
        }

        if (candidate.RequestBody is null)
        {
            findings.Add(new Finding(
                FindingSeverity.Breaking,
                "REQUEST_BODY_REMOVED",
                location,
                "A request body accepted by the previous contract is no longer described."));
            return;
        }

        if (!baseline.RequestBodyRequired && candidate.RequestBodyRequired)
        {
            findings.Add(new Finding(
                FindingSeverity.Breaking,
                "REQUEST_BODY_NOW_REQUIRED",
                location,
                "The request body changed from optional to required."));
        }
        else if (baseline.RequestBodyRequired && !candidate.RequestBodyRequired)
        {
            findings.Add(new Finding(
                FindingSeverity.NonBreaking,
                "REQUEST_BODY_NOW_OPTIONAL",
                location,
                "The request body changed from required to optional."));
        }

        CompareSchemas(baseline.RequestBody, candidate.RequestBody, location, SchemaDirection.Request, findings);
    }

    private static void CompareResponses(
        string operationKey,
        OperationContract baseline,
        OperationContract candidate,
        List<Finding> findings)
    {
        foreach (var (status, oldSchema) in baseline.Responses)
        {
            var location = $"{operationKey} response {status}";
            if (!candidate.Responses.TryGetValue(status, out var newSchema))
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    "RESPONSE_REMOVED",
                    location,
                    "A response status described by the previous contract was removed."));
                continue;
            }

            if (oldSchema is not null && newSchema is null)
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    "RESPONSE_BODY_REMOVED",
                    location,
                    "The response body schema was removed."));
            }
            else if (oldSchema is null && newSchema is not null)
            {
                findings.Add(new Finding(
                    FindingSeverity.NonBreaking,
                    "RESPONSE_BODY_ADDED",
                    location,
                    "A response body schema was added."));
            }
            else if (oldSchema is not null && newSchema is not null)
            {
                CompareSchemas(oldSchema, newSchema, location, SchemaDirection.Response, findings);
            }
        }

        foreach (var (status, _) in candidate.Responses)
        {
            if (!baseline.Responses.ContainsKey(status))
            {
                findings.Add(new Finding(
                    FindingSeverity.NonBreaking,
                    "RESPONSE_ADDED",
                    $"{operationKey} response {status}",
                    "A new response status was added."));
            }
        }
    }

    private static void CompareSchemas(
        SchemaContract? baseline,
        SchemaContract? candidate,
        string location,
        SchemaDirection direction,
        List<Finding> findings)
    {
        if (baseline is null || candidate is null)
        {
            return;
        }

        foreach (var unsupported in baseline.Unsupported.Concat(candidate.Unsupported).Distinct(StringComparer.Ordinal))
        {
            findings.Add(new Finding(
                FindingSeverity.Warning,
                "UNSUPPORTED_SCHEMA_FEATURE",
                location,
                $"Compatibility for '{unsupported}' is not evaluated."));
        }

        if (!string.Equals(baseline.Format, candidate.Format, StringComparison.OrdinalIgnoreCase)
            && (baseline.Format is not null || candidate.Format is not null))
        {
            findings.Add(new Finding(
                FindingSeverity.Warning,
                "FORMAT_CHANGED",
                location,
                $"The schema format changed from '{baseline.Format ?? "unspecified"}' to '{candidate.Format ?? "unspecified"}'; format compatibility is not inferred."));
        }

        if (baseline.Types.Count > 0 && candidate.Types.Count > 0)
        {
            var narrowed = direction == SchemaDirection.Request
                ? !candidate.Types.IsSupersetOf(baseline.Types)
                : !baseline.Types.IsSupersetOf(candidate.Types);
            if (narrowed)
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    "SCHEMA_TYPE_RESTRICTED",
                    location,
                    direction == SchemaDirection.Request
                        ? "The new request schema accepts fewer value types."
                        : "The new response schema may return value types not accepted by the previous contract."));
            }
            else if (!baseline.Types.SetEquals(candidate.Types))
            {
                findings.Add(new Finding(
                    FindingSeverity.NonBreaking,
                    direction == SchemaDirection.Request ? "SCHEMA_TYPE_WIDENED" : "RESPONSE_TYPE_NARROWED",
                    location,
                    direction == SchemaDirection.Request
                        ? "The request schema now accepts a wider set of value types."
                        : "The response schema now returns a narrower set of value types."));
            }
        }
        else if (baseline.Types.Count != candidate.Types.Count)
        {
            findings.Add(new Finding(
                FindingSeverity.Warning,
                "SCHEMA_TYPE_UNSPECIFIED",
                location,
                "A schema has an unspecified type; type compatibility cannot be confirmed."));
        }

        if (baseline.EnumValues is not null && candidate.EnumValues is not null)
        {
            var narrowed = direction == SchemaDirection.Request
                ? !candidate.EnumValues.IsSupersetOf(baseline.EnumValues)
                : !baseline.EnumValues.IsSupersetOf(candidate.EnumValues);
            if (narrowed)
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    direction == SchemaDirection.Request ? "ENUM_VALUE_REMOVED" : "RESPONSE_ENUM_VALUE_ADDED",
                    location,
                    direction == SchemaDirection.Request
                        ? "One or more values accepted by the previous request enum are no longer accepted."
                        : "The response enum may now contain values not accepted by the previous contract."));
            }
            else if (!baseline.EnumValues.SetEquals(candidate.EnumValues))
            {
                findings.Add(new Finding(
                    FindingSeverity.NonBreaking,
                    direction == SchemaDirection.Request ? "ENUM_VALUE_ADDED" : "RESPONSE_ENUM_VALUE_REMOVED",
                    location,
                    direction == SchemaDirection.Request
                        ? "The request enum now accepts additional values."
                        : "The response enum now returns a subset of the previous values."));
            }
        }
        else if ((baseline.EnumValues is null) != (candidate.EnumValues is null))
        {
            findings.Add(new Finding(
                FindingSeverity.Warning,
                "ENUM_CONSTRAINT_CHANGED",
                location,
                "An enum constraint was added or removed; value compatibility cannot be fully confirmed."));
        }

        if (direction == SchemaDirection.Request
            && baseline.AdditionalPropertiesAllowed != false
            && candidate.AdditionalPropertiesAllowed == false)
        {
            findings.Add(new Finding(
                FindingSeverity.Breaking,
                "ADDITIONAL_PROPERTIES_REJECTED",
                location,
                "The new request schema rejects additional properties."));
        }

        foreach (var (name, oldProperty) in baseline.Properties)
        {
            var propertyLocation = $"{location}.{name}";
            if (!candidate.Properties.TryGetValue(name, out var newProperty))
            {
                if (direction == SchemaDirection.Response)
                {
                    findings.Add(new Finding(
                        FindingSeverity.Breaking,
                        "RESPONSE_PROPERTY_REMOVED",
                        propertyLocation,
                        "A response property was removed."));
                }
                else if (candidate.AdditionalPropertiesAllowed == false)
                {
                    findings.Add(new Finding(
                        FindingSeverity.Breaking,
                        "REQUEST_PROPERTY_REJECTED",
                        propertyLocation,
                        "The request property was removed and additional properties are rejected."));
                }
                else
                {
                    findings.Add(new Finding(
                        FindingSeverity.Warning,
                        "REQUEST_PROPERTY_REMOVED",
                        propertyLocation,
                        "The request property was removed; whether existing clients are affected depends on server handling of unknown fields."));
                }

                continue;
            }

            var oldRequired = baseline.Required.Contains(name);
            var newRequired = candidate.Required.Contains(name);
            if (direction == SchemaDirection.Request && !oldRequired && newRequired)
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    "PROPERTY_NOW_REQUIRED",
                    propertyLocation,
                    "The request property changed from optional to required."));
            }
            else if (direction == SchemaDirection.Response && oldRequired && !newRequired)
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    "RESPONSE_PROPERTY_NO_LONGER_REQUIRED",
                    propertyLocation,
                    "The response property is no longer guaranteed to be present."));
            }
            else if ((direction == SchemaDirection.Request && oldRequired && !newRequired)
                || (direction == SchemaDirection.Response && !oldRequired && newRequired))
            {
                findings.Add(new Finding(
                    FindingSeverity.NonBreaking,
                    "PROPERTY_REQUIREDNESS_RELAXED",
                    propertyLocation,
                    "The property requiredness changed in a compatible direction."));
            }

            CompareSchemas(oldProperty, newProperty, propertyLocation, direction, findings);
        }

        foreach (var (name, _) in candidate.Properties)
        {
            if (baseline.Properties.ContainsKey(name))
            {
                continue;
            }

            if (direction == SchemaDirection.Request && candidate.Required.Contains(name))
            {
                findings.Add(new Finding(
                    FindingSeverity.Breaking,
                    "REQUIRED_PROPERTY_ADDED",
                    $"{location}.{name}",
                    "A new required request property prevents existing clients from sending the previous payload unchanged."));
            }
            else
            {
                findings.Add(new Finding(
                    FindingSeverity.NonBreaking,
                    direction == SchemaDirection.Request ? "OPTIONAL_PROPERTY_ADDED" : "RESPONSE_PROPERTY_ADDED",
                    $"{location}.{name}",
                    direction == SchemaDirection.Request
                        ? "An optional request property was added."
                        : "A response property was added."));
            }
        }

        if (baseline.Items is not null && candidate.Items is not null)
        {
            CompareSchemas(baseline.Items, candidate.Items, $"{location}[]", direction, findings);
        }
        else if ((baseline.Items is null) != (candidate.Items is null))
        {
            findings.Add(new Finding(
                FindingSeverity.Warning,
                "ARRAY_ITEMS_CHANGED",
                location,
                "Array item schema presence changed; compatibility cannot be confirmed."));
        }
    }

    private enum SchemaDirection
    {
        Request,
        Response
    }
}
