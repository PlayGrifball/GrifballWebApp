﻿#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class SignupAvailability
{
    public int SeasonSignupID { get; set; }
    public int AvailabilityOptionID { get; set; }
    public SeasonSignup SeasonSignup { get; set; }
    public AvailabilityOption AvailabilityOption { get; set; }
    public SignupAvailability Copy()
    {
        return new SignupAvailability()
        {
            AvailabilityOptionID = AvailabilityOptionID,
            AvailabilityOption = AvailabilityOption,
        };
    }
}
