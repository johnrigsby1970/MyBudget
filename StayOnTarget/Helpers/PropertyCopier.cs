namespace StayOnTarget.Helpers;

public static class PropertyCopier
{
    public static void CopyProperties<TSource, TTarget>(TSource source, TTarget target)
    {
        if (source == null || target == null) return; // Safeguard for null inputs

        var sourceProps = typeof(TSource).GetProperties();
        var targetProps = typeof(TTarget).GetProperties();

        foreach (var sourceProp in sourceProps)
        {
            if (!sourceProp.CanRead) continue;

            // Optional: Skip indexed properties (e.g. public string this[int index]) to avoid GetValue errors
            if (sourceProp.GetIndexParameters().Length > 0) continue;

            var targetProp = targetProps.FirstOrDefault(p => 
                p.Name == sourceProp.Name && 
                p.PropertyType == sourceProp.PropertyType && 
                p.CanWrite);

            if (targetProp != null)
            {
                targetProp.SetValue(target, sourceProp.GetValue(source));
            }
        }
    }
}