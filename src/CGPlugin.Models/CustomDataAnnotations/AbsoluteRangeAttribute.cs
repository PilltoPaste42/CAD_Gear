﻿namespace CGPlugin.Models.CustomDataAnnotations;

using System;
using System.ComponentModel.DataAnnotations;

/// <summary>
///     Настройка ограничения абсолютного численного значения для поля данных
/// </summary>
public class AbsoluteRangeAttribute : ValidationAttribute
{
    /// <param name="minimum"> Абсолютный минимум</param>
    /// <param name="maximum"> Абсолютный максимум</param>
    public AbsoluteRangeAttribute(int minimum, int maximum)
    {
        Maximum = Math.Abs(maximum);
        Minimum = Math.Abs(minimum);
    }

    public int Maximum { get; set; }
    public int Minimum { get; set; }

    ///<inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value == null)
        {
            return false;
        }

        var absVal = Math.Abs((int)value);

        return absVal >= Minimum && absVal <= Maximum;
    }

    ///<inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var defaultErrorMessage =
            $"Absolute value of field {validationContext.DisplayName} must be between {Minimum} and {Maximum}.";

        if (!IsValid(value))
        {
            return new ValidationResult(ErrorMessage ?? defaultErrorMessage);
        }

        return ValidationResult.Success;
    }
}