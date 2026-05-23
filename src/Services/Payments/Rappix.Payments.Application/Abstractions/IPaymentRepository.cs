using Rappix.Payments.Domain.Payments;

namespace Rappix.Payments.Application.Abstractions;

/// <summary>
/// Repositorio del aggregate <see cref="Payment"/>. Los consumers (Authorize/Capture/Void/Refund)
/// cargan por <c>OrderId</c>, mutan via metodos del aggregate y guardan via <see cref="IUnitOfWork"/>.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>Recupera el pago por <c>OrderId</c> (= identidad del aggregate). Null si no existe.</summary>
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Lo marca para insercion al proximo SaveChanges.</summary>
    void Add(Payment payment);
}
