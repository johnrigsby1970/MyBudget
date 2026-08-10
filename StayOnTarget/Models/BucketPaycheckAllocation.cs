using System;
using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class BucketPaycheckAllocation : ViewModelBase
{
    private int _allocationId;
    private int _bucketId;
    private int _paycheckId;
    private string _allocationType = "FixedAmount"; // FixedAmount or Percentage
    private decimal _allocationValue;
    private int _sortOrder;
    private bool _isActive = true;
    private DateTime _createdDate;
    private string? _paycheckName;

    public int AllocationId
    {
        get => _allocationId;
        set => SetProperty(ref _allocationId, value);
    }

    public int BucketId
    {
        get => _bucketId;
        set => SetProperty(ref _bucketId, value);
    }

    public int PaycheckId
    {
        get => _paycheckId;
        set => SetProperty(ref _paycheckId, value);
    }

    public string AllocationType
    {
        get => _allocationType;
        set => SetProperty(ref _allocationType, value);
    }

    public decimal AllocationValue
    {
        get => _allocationValue;
        set => SetProperty(ref _allocationValue, value);
    }

    public int SortOrder
    {
        get => _sortOrder;
        set => SetProperty(ref _sortOrder, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public DateTime CreatedDate
    {
        get => _createdDate;
        set => SetProperty(ref _createdDate, value);
    }

    public string? PaycheckName
    {
        get => _paycheckName;
        set => SetProperty(ref _paycheckName, value);
    }
}