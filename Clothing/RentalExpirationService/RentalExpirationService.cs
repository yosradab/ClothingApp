using Clothing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class RentalExpirationService : IHostedService, IDisposable
{
    private readonly ILogger<RentalExpirationService> _logger;
    private readonly IServiceProvider _services;
    private Timer _timer;

    public RentalExpirationService(ILogger<RentalExpirationService> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rental Expiration Service is starting.");

        // Schedule the task to run every day at a specific time (e.g., midnight).
        // For demonstration, we'll run it every 1 minute.
        // In a real application, you might use a more robust scheduling library.
        _timer = new Timer(CheckExpiredRentals, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

        return Task.CompletedTask;
    }

    private async void CheckExpiredRentals(object state)
    {
        _logger.LogInformation("Checking for expired rentals...");

        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClothingPlatformContext>();

            try
            {
                var expiredAnnonces = await context.Annonces
                    .Where(a => a.Statut == "Loué" && a.Type == TypeAnnonce.Location && a.DateFinLocation.HasValue && a.DateFinLocation.Value < DateTime.UtcNow)
                    .ToListAsync();

                foreach (var annonce in expiredAnnonces)
                {
                    annonce.Statut = "Disponible";
                    annonce.AcheteurId = null;
                    annonce.DateDebutLocation = null;
                    annonce.DateFinLocation = null;

                    _logger.LogInformation($"Ad ID {annonce.Id} has been made available again after rental expiration.");
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking for expired rentals.");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rental Expiration Service is stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}