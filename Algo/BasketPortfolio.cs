namespace StockSharp.Algo
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Linq;

	using Ecng.Common;
	using Ecng.Collections;

	using StockSharp.BusinessEntities;
	using StockSharp.Messages;

	/// <summary>
	/// Basket portfolio.
	/// </summary>
	public abstract class BasketPortfolio : Portfolio
	{
		/// <summary>
		/// Portfolios from which this basket is created.
		/// </summary>
		[Browsable(false)]
		public abstract IEnumerable<Portfolio> InnerPortfolios { get; }

		/// <summary>
		/// Positions from which this basket is created.
		/// </summary>
		[Browsable(false)]
		public abstract IEnumerable<BasketPosition> InnerPositions { get; }
	}

	/// <summary>
	/// Portfolios basket based on the weights <see cref="WeightedPortfolio.Weights"/>.
	/// </summary>
	public class WeightedPortfolio : BasketPortfolio
	{
		private sealed class WeightsDictionary : CachedSynchronizedDictionary<Portfolio, decimal>
		{
			private sealed class WeightedPosition : BasketPosition
			{
				public WeightedPosition(WeightedPortfolio portfolio, IEnumerable<Position> innerPositions)
				{
					if (innerPositions == null)
						throw new ArgumentNullException(nameof(innerPositions));

					_innerPositions = innerPositions;

					var beginValue = 0m;
					var currentValue = 0m;
					var blockedValue = 0m;

					foreach (var position in _innerPositions)
					{
						var mult = portfolio.Weights[position.Portfolio];

						beginValue += mult * position.BeginValue;
						currentValue += mult * position.CurrentValue;
						blockedValue += mult * position.BlockedValue;
					}

					BeginValue = beginValue;
					BlockedValue = blockedValue;
					CurrentValue = currentValue;
				}

				private readonly IEnumerable<Position> _innerPositions; 

				public override IEnumerable<Position> InnerPositions => _innerPositions;
			}

			private readonly WeightedPortfolio _parent;
			private readonly IConnector _connector;

			public WeightsDictionary(WeightedPortfolio parent, IConnector connector)
			{
				if (parent == null)
					throw new ArgumentNullException(nameof(parent));

				_parent = parent;
				_connector = connector;
			}

			public IEnumerable<BasketPosition> Positions
			{
				get
				{
					return CachedKeys
								.SelectMany(pf => _connector.Positions.Where(pos => pos.Portfolio == pf))
								.GroupBy(pos => pos.Security)
								.Select(g => new WeightedPosition(_parent, g));
				}
			}

			public override void Add(Portfolio key, decimal value)
			{
				base.Add(key, value);
				((INotifyPropertyChanged)key).PropertyChanged += OnPortfolioChanged;
				RefreshName();
			}

			public override bool Remove(Portfolio key)
			{
				if (base.Remove(key))
				{
					((INotifyPropertyChanged)key).PropertyChanged -= OnPortfolioChanged;
					RefreshName();
					return true;
				}

				return false;
			}

			public override void Clear()
			{
				foreach (var portfolio in CachedKeys)
					Remove(portfolio);
			}

			private void OnPortfolioChanged(object sender, PropertyChangedEventArgs e)
			{
				RefreshParent();
			}

			private void RefreshName()
			{
				_parent.Name = CachedPairs.Select(p => "{0}*{1}".Put(p.Value, p.Key)).Join(", ");
				RefreshParent();
			}

			private void RefreshParent()
			{
				var currencyType = _parent.Currency;

				var beginValue = 0m.ToCurrency(currencyType ?? CurrencyTypes.USD);
				var currentValue = 0m.ToCurrency(currencyType ?? CurrencyTypes.USD);
				var leverage = 0m.ToCurrency(currencyType ?? CurrencyTypes.USD);
				var commission = 0m.ToCurrency(currencyType ?? CurrencyTypes.USD);

				foreach (var pair in CachedPairs)
				{
					var portfolio = pair.Key;
					var weight = (Currency)pair.Value;

					beginValue += Multiple(beginValue, weight, portfolio.BeginValue);
					currentValue += Multiple(currentValue, weight, portfolio.CurrentValue);
					leverage += Multiple(leverage, weight, portfolio.Leverage);
					commission += Multiple(commission, weight, portfolio.Commission);
				}

				_parent.BeginValue = beginValue.Value;
				_parent.CurrentValue = currentValue.Value;
				_parent.Leverage = leverage.Value / Count;
				_parent.Commission = commission.Value;
			}

			private static Currency Multiple(Currency currency, Currency weight, Currency part)
			{
				if (currency == null)
					throw new ArgumentNullException(nameof(currency));

				if (part == null)
					throw new ArgumentNullException(nameof(part));

				if (currency.Type != part.Type)
					part = part.Convert(currency.Type);

				return currency * weight * part;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WeightedPortfolio"/>.
		/// </summary>
		/// <param name="connector">The connection of interaction with trade systems.</param>
		public WeightedPortfolio(IConnector connector)
		{
			_weights = new WeightsDictionary(this, connector);
		}

		private readonly WeightsDictionary _weights;

		/// <summary>
		/// Instruments and their weighting coefficients in the basket.
		/// </summary>
		public SynchronizedDictionary<Portfolio, decimal> Weights => _weights;

		/// <summary>
		/// Portfolios from which this basket is created.
		/// </summary>
		public override IEnumerable<Portfolio> InnerPortfolios => _weights.CachedKeys;

		/// <summary>
		/// Positions from which this basket is created.
		/// </summary>
		public override IEnumerable<BasketPosition> InnerPositions => _weights.Positions;
	}
}