using Microsoft.EntityFrameworkCore;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Domain.Payments;

namespace Rappix.Payments.Infrastructure.Persistence;

/// <summary>
/// Repositorio del aggregate <see cref="Payment"/>. Los consumers cargan por <c>OrderId</c>
/// (= identidad del aggregate, ver ADR-0009 #4), mutan via metodos del aggregate y guardan via
/// <see cref="IUnitOfWork"/>.
/// </summary>
internal sealed class PaymentRepository(PaymentsDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Payments.FirstOrDefaultAsync(payment => payment.Id == orderId, cancellationToken);

    public void Add(Payment payment) => db.Payments.Add(payment);
}
