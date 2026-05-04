public class ReservationTripAvailabilityModel
{
    public int OutboundAvailable { get; set; }
    public int ReturnAvailable { get; set; }

    public int OutboundSelected { get; set; }
    public int ReturnSelected { get; set; }

    public bool HasOutboundCapacity => OutboundAvailable >= OutboundSelected;
    public bool HasReturnCapacity => ReturnAvailable >= ReturnSelected;
}