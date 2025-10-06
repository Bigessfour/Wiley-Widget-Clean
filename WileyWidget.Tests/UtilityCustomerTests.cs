using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for the UtilityCustomer model validation, business logic, and property changes
/// </summary>
public class UtilityCustomerTests
{
    [Fact]
    public void UtilityCustomer_Creation_WithValidData_Succeeds()
    {
        // Arrange & Act
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        // Assert
        Assert.Equal("ACC001", customer.AccountNumber);
        Assert.Equal("John", customer.FirstName);
        Assert.Equal("Doe", customer.LastName);
        Assert.Equal("John Doe", customer.FullName);
        Assert.Equal("John Doe", customer.DisplayName); // No company name
        Assert.Equal("123 Main St", customer.ServiceAddress);
        Assert.Equal("Anytown", customer.ServiceCity);
        Assert.Equal("CA", customer.ServiceState);
        Assert.Equal("12345", customer.ServiceZipCode);
        Assert.Equal(CustomerType.Residential, customer.CustomerType);
        Assert.Equal("Residential", customer.CustomerTypeDescription);
        Assert.Equal(ServiceLocation.InsideCityLimits, customer.ServiceLocation);
        Assert.Equal("Inside City Limits", customer.ServiceLocationDescription);
        Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.Equal("Active", customer.StatusDescription);
        Assert.True(customer.IsActive);
        Assert.True(customer.CreatedDate <= DateTime.Now);
        Assert.True(customer.LastModifiedDate <= DateTime.Now);
    }

    [Fact]
    public void UtilityCustomer_WithCompanyName_UsesCompanyAsDisplayName()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Doe",
            CompanyName = "ABC Corp",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Commercial,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        // Assert
        Assert.Equal("John Doe", customer.FullName);
        Assert.Equal("ABC Corp", customer.DisplayName); // Company name takes precedence
    }

    [Theory]
    [InlineData("", false)]           // Empty account number
    [InlineData(null, false)]        // Null account number
    [InlineData("ACC001", true)]     // Valid account number
    [InlineData("A", true)]          // Minimum valid length
    public void UtilityCustomer_AccountNumber_Validation(string accountNumber, bool shouldBeValid)
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = accountNumber,
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        if (shouldBeValid)
        {
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
        else
        {
            Assert.False(isValid);
            Assert.Contains(validationResults, vr => vr.MemberNames.Contains(nameof(UtilityCustomer.AccountNumber)));
        }
    }

    [Theory]
    [InlineData("", false)]           // Empty first name
    [InlineData(null, false)]        // Null first name
    [InlineData("John", true)]       // Valid first name
    [InlineData("A", true)]          // Minimum valid length
    public void UtilityCustomer_FirstName_Validation(string firstName, bool shouldBeValid)
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = firstName,
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        if (shouldBeValid)
        {
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
        else
        {
            Assert.False(isValid);
            Assert.Contains(validationResults, vr => vr.MemberNames.Contains(nameof(UtilityCustomer.FirstName)));
        }
    }

    [Theory]
    [InlineData("", false)]           // Empty last name
    [InlineData(null, false)]        // Null last name
    [InlineData("Doe", true)]        // Valid last name
    [InlineData("A", true)]          // Minimum valid length
    public void UtilityCustomer_LastName_Validation(string lastName, bool shouldBeValid)
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = "John",
            LastName = lastName,
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            EmailAddress = "test@example.com",
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        if (shouldBeValid)
        {
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
        else
        {
            Assert.False(isValid);
            Assert.Contains(validationResults, vr => vr.MemberNames.Contains(nameof(UtilityCustomer.LastName)));
        }
    }

    [Theory]
    [InlineData("", false)]           // Empty service address
    [InlineData(null, false)]        // Null service address
    [InlineData("123 Main St", true)] // Valid service address
    public void UtilityCustomer_ServiceAddress_Validation(string serviceAddress, bool shouldBeValid)
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = serviceAddress,
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        if (shouldBeValid)
        {
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
        else
        {
            Assert.False(isValid);
            Assert.Contains(validationResults, vr => vr.MemberNames.Contains(nameof(UtilityCustomer.ServiceAddress)));
        }
    }

    [Theory]
    [InlineData("", false)]           // Empty state
    [InlineData(null, false)]        // Null state
    [InlineData("C", false)]         // Too short
    [InlineData("CA", true)]         // Valid 2-character state
    [InlineData("CAL", false)]       // Too long
    public void UtilityCustomer_ServiceState_Validation(string serviceState, bool shouldBeValid)
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = serviceState,
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        if (shouldBeValid)
        {
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
        else
        {
            Assert.False(isValid);
            Assert.Contains(validationResults, vr => vr.MemberNames.Contains(nameof(UtilityCustomer.ServiceState)));
        }
    }

    [Theory]
    [InlineData("valid@email.com", true)]     // Valid email
    [InlineData("invalid-email", false)]     // Invalid email
    [InlineData("", true)]                   // Empty email (optional)
    [InlineData(null, true)]                 // Null email (optional)
    public void UtilityCustomer_EmailAddress_Validation(string emailAddress, bool shouldBeValid)
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            EmailAddress = emailAddress,
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        // Act
        var validationContext = new ValidationContext(customer);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, validationContext, validationResults, true);

        // Assert
        if (shouldBeValid)
        {
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
        else
        {
            Assert.False(isValid);
            Assert.Contains(validationResults, vr => vr.MemberNames.Contains(nameof(UtilityCustomer.EmailAddress)));
        }
    }

    [Fact]
    public void UtilityCustomer_PropertyChanged_Events_Are_Raised()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        var propertyChangedEvents = new List<string>();

        customer.PropertyChanged += (sender, args) =>
        {
            propertyChangedEvents.Add(args.PropertyName);
        };

        // Act
        customer.FirstName = "Jane";
        customer.LastName = "Smith";
        customer.CompanyName = "ABC Corp";
        customer.Status = CustomerStatus.Inactive;

        // Assert
        Assert.Contains(nameof(UtilityCustomer.FirstName), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.LastName), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.FullName), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.DisplayName), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.CompanyName), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.Status), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.StatusDescription), propertyChangedEvents);
        Assert.Contains(nameof(UtilityCustomer.IsActive), propertyChangedEvents);
    }

    [Theory]
    [InlineData(CustomerType.Residential, "Residential")]
    [InlineData(CustomerType.Commercial, "Commercial")]
    [InlineData(CustomerType.Industrial, "Industrial")]
    [InlineData(CustomerType.Institutional, "Institutional")]
    [InlineData(CustomerType.Government, "Government")]
    [InlineData(CustomerType.MultiFamily, "Multi-Family")]
    public void UtilityCustomer_CustomerTypeDescription_Returns_Correct_Value(CustomerType customerType, string expectedDescription)
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = customerType,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        // Assert
        Assert.Equal(expectedDescription, customer.CustomerTypeDescription);
    }

    [Theory]
    [InlineData(ServiceLocation.InsideCityLimits, "Inside City Limits")]
    [InlineData(ServiceLocation.OutsideCityLimits, "Outside City Limits")]
    public void UtilityCustomer_ServiceLocationDescription_Returns_Correct_Value(ServiceLocation serviceLocation, string expectedDescription)
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = serviceLocation,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now
        };

        // Assert
        Assert.Equal(expectedDescription, customer.ServiceLocationDescription);
    }

    [Theory]
    [InlineData(CustomerStatus.Active, "Active", true)]
    [InlineData(CustomerStatus.Inactive, "Inactive", false)]
    [InlineData(CustomerStatus.Suspended, "Suspended", false)]
    [InlineData(CustomerStatus.Closed, "Closed", false)]
    public void UtilityCustomer_StatusDescription_And_IsActive_Return_Correct_Values(CustomerStatus status, string expectedDescription, bool expectedIsActive)
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = status,
            AccountOpenDate = DateTime.Now
        };

        // Assert
        Assert.Equal(expectedDescription, customer.StatusDescription);
        Assert.Equal(expectedIsActive, customer.IsActive);
    }

    [Fact]
    public void UtilityCustomer_CurrentBalance_Formats_Correctly()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            AccountNumber = "ACC001",
            FirstName = "John",
            LastName = "Doe",
            ServiceAddress = "123 Main St",
            ServiceCity = "Anytown",
            ServiceState = "CA",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.Now,
            CurrentBalance = 123.45m
        };

        // Assert
        Assert.Equal("$123.45", customer.FormattedBalance);
    }

    [Fact]
    public void UtilityCustomer_FullName_Combines_First_And_Last_Name()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Doe"
        };

        // Assert
        Assert.Equal("John Doe", customer.FullName);
    }

    [Fact]
    public void UtilityCustomer_FullName_Handles_Empty_Names()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "",
            LastName = ""
        };

        // Assert
        Assert.Equal("", customer.FullName.Trim());
    }

    [Fact]
    public void UtilityCustomer_DisplayName_Uses_Company_When_Available()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Doe",
            CompanyName = "ABC Corp"
        };

        // Assert
        Assert.Equal("ABC Corp", customer.DisplayName);
    }

    [Fact]
    public void UtilityCustomer_DisplayName_Falls_Back_To_FullName_When_No_Company()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Doe",
            CompanyName = ""
        };

        // Assert
        Assert.Equal("John Doe", customer.DisplayName);
    }

    [Fact]
    public void UtilityCustomer_Default_Values_Are_Set_Correctly()
    {
        // Arrange
        var customer = new UtilityCustomer();

        // Assert
        Assert.Equal(string.Empty, customer.AccountNumber);
        Assert.Equal(string.Empty, customer.FirstName);
        Assert.Equal(string.Empty, customer.LastName);
        Assert.Equal(string.Empty, customer.CompanyName);
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
        Assert.Equal(string.Empty, customer.MeterNumber);
        Assert.Equal(string.Empty, customer.TaxId);
        Assert.Equal(string.Empty, customer.BusinessLicenseNumber);
        Assert.Equal(string.Empty, customer.Notes);
        Assert.Equal(0m, customer.CurrentBalance);
        Assert.Null(customer.AccountCloseDate);
        Assert.True(customer.CreatedDate <= DateTime.Now);
        Assert.True(customer.LastModifiedDate <= DateTime.Now);
    }
}