namespace StayOnTarget.Helpers;

public static class ReflectionExtensions
{
    public static T CloneReflection<T>(this T source) where T : new()
    {
        if (source == null) return default!;
        
        var clone = new T();
        CopyProperties(source, clone);
        return clone;
    }

    public static void CopyProperties<TSource, TTarget>(TSource source, TTarget target)
    {
        if (source == null || target == null) return;

        var sourceProps = typeof(TSource).GetProperties();
        var targetProps = typeof(TTarget).GetProperties();

        foreach (var sourceProp in sourceProps)
        {
            if (!sourceProp.CanRead) continue;
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