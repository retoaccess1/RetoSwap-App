using Haveno.Proto.Grpc;
using Manta.Helpers;
using Microsoft.AspNetCore.Components;

using static Haveno.Proto.Grpc.Offers;

namespace Manta.Components.Pages;

public partial class MyOffers : ComponentBase
{
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    public List<OfferInfo> Offers { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        while (true)
        {
            try
            {
                var grpcChannelHelper = new GrpcChannelHelper();
                var offersClient = new OffersClient(grpcChannelHelper.Channel);

                var getMyOffersResponse = await offersClient.GetMyOffersAsync(new GetMyOffersRequest
                {

                });

                Offers = [.. getMyOffersResponse.Offers];

                // Why does this not get set?
                Offers.ForEach(x => x.IsMyOffer = true);

                break;
            }
            catch
            {

            }

            await Task.Delay(5_000);
        }

        await base.OnInitializedAsync();
    }

    public async Task CancelOfferAsync(string id)
    {
        try
        {
            var grpcChannelHelper = new GrpcChannelHelper();
            var offersClient = new OffersClient(grpcChannelHelper.Channel);

            var cancelOfferResponse = await offersClient.CancelOfferAsync(new CancelOfferRequest
            {
                Id = id
            });

            var offer = Offers.FirstOrDefault(x => x.Id == id);
            if (offer is null)
                return;

            Offers.Remove(offer);
        }
        catch
        {

        }
    }

    public void EditOffer()
    {
        NavigationManager.NavigateTo("/buysell?title=Buy%20%26%20Sell");
    }
}
