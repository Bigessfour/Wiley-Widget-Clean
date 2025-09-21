using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests.Models;

/// <summary>
/// Comprehensive tests for UtilityCustomer model
/// Tests INotifyPropertyChanged, calculated properties, validation, enums, and business logic
/// </summary>
public class UtilityCustomerTests
{
    [Fact]
    public void UtilityCustomer_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var customer = new UtilityCustomer();

        // Assert
        Assert.Equal(0, customer.Id);
        Assert.Equal(string.Empty, customer.AccountNumber);
        Assert.Equal(string.Empty, customer.FirstName);
        Assert.Equal(string.Empty, customer.LastName);
        Assert.Equal(string.Empty, customer.CompanyName);
        Assert.Equal(CustomerType.Residential, customer.CustomerType); // Default enum value
        Assert.Equal(string.Empty, customer.ServiceAddress);
        Assert.Equal(string.Empty, customer.ServiceCity);
        Assert.Equal(string.Empty, customer.ServiceState);
        Assert.Equal(string.Empty, customer.ServiceZipCode);
        Assert.Equal(string.Empty, customer.MailingAddress);
        Assert.Equal(string.Empty, customer.MailingCity);
        Assert.Equal(string.Empty, customer.MailingState);
        Assert.Equal(string.Empty, customer.MailingZipCode);
        Assert.Equal(string.Empty, customer.PhoneNumber);
        Assert.Equal(string.Empty, customer.EmailAddress);
        Assert.Equal(ServiceLocation.InsideCityLimits, customer.ServiceLocation); // Default enum value
        Assert.Equal(CustomerStatus.Active, customer.Status); // Default enum value
        Assert.Null(customer.ConnectDate);
        Assert.Null(customer.DisconnectDate);
        Assert.Equal(0m, customer.CurrentBalance);
        Assert.Equal(0m, customer.LastPaymentAmount);
        Assert.Null(customer.LastPaymentDate);
        Assert.Equal(string.Empty, customer.Notes);
    }

    [Fact]
    public void UtilityCustomer_PropertyChanged_EventsAreRaised()
    {
        // Arrange
        var customer = new UtilityCustomer();
        var propertyChangedEvents = new List<string>();

        customer.PropertyChanged += (sender, args) =>
        {
            propertyChangedEvents.Add(args.PropertyName);
        };

        // Act
        customer.AccountNumber = "CUST001";
        customer.FirstName = "John";
        customer.LastName = "Doe";
        customer.CompanyName = "ABC Corp";
        customer.ServiceAddress = "123 Main St";
        customer.ServiceCity = "Anytown";
        customer.ServiceState = "CA";
        customer.ServiceZipCode = "12345";

        // Assert
        Assert.Contains(nameof(UtilityCustomer.AccountNumber), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.FirstName), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.LastName), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.CompanyName), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.ServiceAddress), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.ServiceCity), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.ServiceState), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.ServiceZipCode), propertyChangedEvents);
    }

    [Fact]
    public void UtilityCustomer_PropertyChanged_CalculatedPropertiesAreUpdated()
    {
        // Arrange
        var customer = new UtilityCustomer();
        var propertyChangedEvents = new List<string>();

        customer.PropertyChanged += (sender, args) =>
        {
            propertyChangedEvents.Add(args.PropertyName);
        };

        // Act
        customer.FirstName = "John";
        customer.LastName = "Doe";
        customer.CompanyName = "ABC Corp";

        // Assert
        Assert.Contains(nameof(UtilityCustomer.FullName), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.DisplayName), propertyChangedEvents);
    }

    [Fact]
    public void UtilityCustomer_FullName_CombinesFirstAndLastName()
    {
        // Arrange
        var customer = new UtilityCustomer();

        // Act
        customer.FirstName = "John";
        customer.LastName = "Doe";

        // Assert
        Assert.Equal("John Doe", customer.FullName);
    }

    [Fact]
    public void UtilityCustomer_FullName_HandlesEmptyValues()
    {
        // Arrange
        var customer = new UtilityCustomer();

        // Act
        customer.FirstName = "";
        customer.LastName = "Doe";

        // Assert
        Assert.Equal("Doe", customer.FullName);

        // Act
        customer.FirstName = "John";
        customer.LastName = "";

        // Assert
        Assert.Equal("John", customer.FullName);

        // Act
        customer.FirstName = "";
        customer.LastName = "";

        // Assert
        Assert.Equal("", customer.FullName);
    }

    [Fact]
    public void UtilityCustomer_DisplayName_UsesCompanyNameWhenAvailable()
    {
        // Arrange
        var customer = new UtilityCustomer();

        // Act
        customer.FirstName = "John";
        customer.LastName = "Doe";
        customer.CompanyName = "ABC Corporation";

        // Assert
        Assert.Equal("ABC Corporation", customer.DisplayName);
    }

    [Fact]
    public void UtilityCustomer_DisplayName_UsesFullNameWhenNoCompany()
    {
        // Arrange
        var customer = new UtilityCustomer();

        // Act
        customer.FirstName = "John";
        customer.LastName = "Doe";
        customer.CompanyName = "";

        // Assert
        Assert.Equal("John Doe", customer.DisplayName);
    }

    [Fact]
    public void UtilityCustomer_DisplayName_UsesFullNameWhenCompanyIsWhitespace()
    {
        // Arrange
        var customer = new UtilityCustomer();

        // Act
        customer.FirstName = "John";
        customer.LastName = "Doe";
        customer.CompanyName = "   "; // Whitespace only

        // Assert
        Assert.Equal("John Doe", customer.DisplayName);
    }

    [Theory]
    [InlineData(CustomerType.Residential, "Residential")]
    [InlineData(CustomerType.Commercial, "Commercial")]
    [InlineData(CustomerType.Industrial, "Industrial")]
    [InlineData(CustomerType.Institutional, "Institutional")]
    [InlineData(CustomerType.Government, "Government")]
    [InlineData(CustomerType.MultiFamily, "Multi-Family")]
    public void UtilityCustomer_CustomerTypeDescription_ReturnsCorrectDescription(CustomerType type, string expectedDescription)
    {
        // Arrange
        var customer = new UtilityCustomer();

        // Act
        customer.CustomerType = type;

        // Assert
        Assert.Equal(expectedDescription, customer.CustomerTypeDescription);
    }

    [Fact]
    public void UtilityCustomer_PropertyChanged_NoEventWhenValueUnchanged()
    {
        // Arrange
        var customer = new UtilityCustomer { FirstName = "John" };
        var eventRaised = false;

        customer.PropertyChanged += (sender, args) => eventRaised = true;

        // Act
        customer.FirstName = "John"; // Same value

        // Assert
        Assert.False(eventRaised);
    }

    [Fact]
    public void UtilityCustomer_Validation_ValidModel_Passes()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void UtilityCustomer_Validation_MissingAccountNumber_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Account number is required");
    }

    [Fact]
    public void UtilityCustomer_Validation_MissingFirstName_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "First name is required");
    }

    [Fact]
    public void UtilityCustomer_Validation_MissingLastName_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = "John",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Last name is required");
    }

    [Fact]
    public void UtilityCustomer_Validation_MissingServiceAddress_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = "John",
            LastName = "Doe",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Service address is required");
    }

    [Fact]
    public void UtilityCustomer_Validation_MissingServiceCity_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceState = "CA",
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Service city is required");
    }

    [Fact]
    public void UtilityCustomer_Validation_MissingServiceState_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Service state is required");
    }

    [Fact]
    public void UtilityCustomer_Validation_MissingServiceZipCode_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Service ZIP code is required");
    }

    [Fact]
    public void UtilityCustomer_Validation_AccountNumberTooLong_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = new string('A', 21), // 21 characters, exceeds max of 20
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Account number cannot exceed 20 characters");
    }

    [Fact]
    public void UtilityCustomer_Validation_FirstNameTooLong_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = new string('A', 51), // 51 characters, exceeds max of 50
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "First name cannot exceed 50 characters");
    }

    [Fact]
    public void UtilityCustomer_Validation_LastNameTooLong_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = "John",
            LastName = new string('A', 51), // 51 characters, exceeds max of 50
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Last name cannot exceed 50 characters");
    }

    [Fact]
    public void UtilityCustomer_Validation_CompanyNameTooLong_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = "John",
            LastName = "Doe",
            CompanyName = new string('A', 101), // 101 characters, exceeds max of 100
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Company name cannot exceed 100 characters");
    }

    [Fact]
    public void UtilityCustomer_Validation_ServiceStateWrongLength_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CAL", // 3 characters, must be exactly 2
            ServiceZipCode = "12345"
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Service state must be exactly 2 characters");
    }

    [Fact]
    public void UtilityCustomer_Validation_ServiceZipCodeTooLong_Fails()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "CUST001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345678901" // 11 characters, exceeds max of 10
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Service ZIP code cannot exceed 10 characters");
    }

    [Fact]
    public void UtilityCustomer_Validation_AllMaxLengths_Pass()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = new string('A', 20), // Exactly 20 characters
            FirstName = new string('A', 50), // Exactly 50 characters
            LastName = new string('A', 50), // Exactly 50 characters
            CompanyName = new string('A', 100), // Exactly 100 characters
            ServiceAddress = new string('A', 200), // Exactly 200 characters
            ServiceCity = new string('A', 50), // Exactly 50 characters
            ServiceState = "CA", // Exactly 2 characters
            ServiceZipCode = new string('1', 10), // Exactly 10 characters
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("John", "Doe", "", "John Doe")]
    [InlineData("Jane", "Smith", "ABC Corp", "ABC Corp")]
    [InlineData("Bob", "Johnson", "   ", "Bob Johnson")]
    public void UtilityCustomer_DisplayName_VariousScenarios(string firstName, string lastName, string companyName, string expectedDisplayName)
    {
        // Arrange
        var customer = new UtilityCustomer();

        // Act
        customer.FirstName = firstName;
        customer.LastName = lastName;
        customer.CompanyName = companyName;

        // Assert
        Assert.Equal(expectedDisplayName, customer.DisplayName);
    }

    [Fact]
    public void UtilityCustomer_Status_DefaultsToActive()
    {
        // Arrange & Act
        var customer = new UtilityCustomer();

        // Assert
        Assert.Equal(CustomerStatus.Active, customer.Status);
    }

    [Fact]
    public void UtilityCustomer_ServiceLocation_DefaultsToInsideCityLimits()
    {
        // Arrange & Act
        var customer = new UtilityCustomer();

        // Assert
        Assert.Equal(ServiceLocation.InsideCityLimits, customer.ServiceLocation);
    }

    [Fact]
    public void UtilityCustomer_CustomerType_DefaultsToResidential()
    {
        // Arrange & Act
        var customer = new UtilityCustomer();

        // Assert
        Assert.Equal(CustomerType.Residential, customer.CustomerType);
    }

    [Fact]
    public void UtilityCustomer_ConnectDate_CanBeSet()
    {
        // Arrange
        var customer = new UtilityCustomer();
        var connectDate = new DateTime(2025, 1, 15);

        // Act
        customer.ConnectDate = connectDate;

        // Assert
        Assert.Equal(connectDate, customer.ConnectDate);
    }

    [Fact]
    public void UtilityCustomer_DisconnectDate_CanBeSet()
    {
        // Arrange
        var customer = new UtilityCustomer();
        var disconnectDate = new DateTime(2025, 9, 19);

        // Act
        customer.DisconnectDate = disconnectDate;

        // Assert
        Assert.Equal(disconnectDate, customer.DisconnectDate);
    }

    [Fact]
    public void UtilityCustomer_LastPaymentDate_CanBeSet()
    {
        // Arrange
        var customer = new UtilityCustomer();
        var paymentDate = new DateTime(2025, 9, 1);

        // Act
        customer.LastPaymentDate = paymentDate;

        // Assert
        Assert.Equal(paymentDate, customer.LastPaymentDate);
    }

    [Fact]
    public void UtilityCustomer_CurrentBalance_CanBeSet()
    {
        // Arrange
        var customer = new UtilityCustomer();

        // Act
        customer.CurrentBalance = 150.75m;

        // Assert
        Assert.Equal(150.75m, customer.CurrentBalance);
    }

    [Fact]
    public void UtilityCustomer_LastPaymentAmount_CanBeSet()
    {
        // Arrange
        var customer = new UtilityCustomer();

        // Act
        customer.LastPaymentAmount = 125.00m;

        // Assert
        Assert.Equal(125.00m, customer.LastPaymentAmount);
    }
}