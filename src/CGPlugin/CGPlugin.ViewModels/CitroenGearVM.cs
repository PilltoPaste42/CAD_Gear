namespace CGPlugin.ViewModels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;

using CGPlugin.Models;
using CGPlugin.Services;
using CGPlugin.Services.Enums;
using CGPlugin.Services.Interfaces;

using CommunityToolkit.Mvvm.Input;

/// <summary>
///   ViewModel для работы с данными из MainWindow
/// </summary>
public class CitroenGearVM : ValidationBase
{
    private readonly CitroenGearModel _gear;
    private readonly IMessageService _message;
    private readonly Dictionary<string, ICollection<string>> _validationErrors;

    public CitroenGearVM()
    {
        _gear = new CitroenGearModel();
        _validationErrors = new Dictionary<string, ICollection<string>>();
        _message = new DisplayMessageService();

        BuildGearCommand = new RelayCommand(BuildCitroenGear, () => ModelIsValid);
        SetDefaultGearCommand = new RelayCommand(SetDefaultGear);
    }

    public RelayCommand BuildGearCommand { get; }

    public uint Diameter
    {
        get => _gear.Diameter;
        set
        {
            _gear.Diameter = value;
            Module = GetModule;
            ValidateModelProperty(value);
            ValidateModelProperty(TeethCount, nameof(TeethCount));
            OnPropertyChanged();
        }
    }

    private uint GetModule
    {
        get
        {
            if (TeethCount == 0)
            {
                return 0;
            }

            return Diameter / TeethCount;
        }
    }

    public override bool HasErrors => !ModelIsValid;

    public bool ModelIsValid =>
        Validator.TryValidateObject(_gear, new ValidationContext(_gear, null, null), null, true);

    public uint Module
    {
        get => _gear.Module;
        set
        {
            _gear.Module = value;
            ValidateModelProperty(value);
            OnPropertyChanged();
        }
    }

    public RelayCommand SetDefaultGearCommand { get; }

    public int TeethAngle
    {
        get => _gear.TeethAngle;
        set
        {
            _gear.TeethAngle = value;
            ValidateModelProperty(value);
            OnPropertyChanged();
        }
    }

    public uint TeethCount
    {
        get => _gear.TeethCount;
        set
        {
            _gear.TeethCount = value;
            Module = GetModule;
            ValidateModelProperty(value);
            OnPropertyChanged();
        }
    }

    public uint Width
    {
        get => _gear.Width;
        set
        {
            _gear.Width = value;
            ValidateModelProperty(value);
            OnPropertyChanged();
        }
    }

    public override IEnumerable GetErrors(string? propertyName = "")
    {
        if (string.IsNullOrEmpty(propertyName))
            return _validationErrors;

        if (!_validationErrors.ContainsKey(propertyName))
            return Enumerable.Empty<string>();

        return _validationErrors[propertyName];
    }

    protected void ValidateModelProperty(object value, [CallerMemberName] string propertyName = "")
    {
        if (_validationErrors.ContainsKey(propertyName))
            _validationErrors.Remove(propertyName);

        ICollection<ValidationResult> validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(_gear, null, null)
        {
            MemberName = propertyName
        };

        if (!Validator.TryValidateProperty(value, validationContext, validationResults))
        {
            _validationErrors.Add(propertyName, new List<string>());
            foreach (var validationResult in validationResults.Where(validationResult =>
                         validationResult.ErrorMessage != null))
            {
                _validationErrors[propertyName].Add(validationResult.ErrorMessage);
            }
        }

        OnErrorsChanged(propertyName);
        BuildGearCommand.NotifyCanExecuteChanged();
    }

    private void BuildCitroenGear()
    {
        if (HasErrors)
        {
            ShowErrorMessage("Gear parameters is not valid!");
            return;
        }

        var builder = new CitroenGearInventorBuilder
        {
            Gear = _gear
        };

        try
        {
            builder.CreateDocument();
            builder.CreateTeethProfile();
            //builder.CreateGearBody();
           // builder.CreateTeeth();
            builder.CreateExtra();
        }
        catch (Exception e)
        {
            ShowErrorMessage(e.Message);
        }
    }

    private void SetDefaultGear()
    {
        Diameter = 200;
        Module = 10;
        TeethAngle = 25;
        TeethCount = 20;
        Width = 50;
    }

    private void ShowErrorMessage(string message)
    {
        const string header = "Error";
        _message.Show(header, message, MessageType.Error);
    }
}