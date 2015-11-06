﻿namespace StockSharp.Studio.Ribbon
{
	using System;
	using System.Windows.Input;

	using StockSharp.Algo;
	using StockSharp.BusinessEntities;
	using StockSharp.Studio.Core.Commands;

	public partial class SecurityGroup
	{
		public readonly static RoutedCommand FindCommand = new RoutedCommand();
		public readonly static RoutedCommand ClearCommand = new RoutedCommand();

		private Security Filter
		{
			get { return (Security)SecurityFilterEditor.SelectedObject; }
			set
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));

				SecurityFilterEditor.SelectedObject = value;
			}
		}

		public SecurityGroup()
		{
			InitializeComponent();

			Filter = new Security();
		}

		private void SecurityCodeLike_OnPreviewKeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Enter)
				return;

			Filter.Code = SecurityCodeLike.Text.Trim();

			if (Filter.IsLookupAll())
				Filter.Code = string.Empty;

			Lookup();
		}

		private void Lookup()
		{
			new LookupSecuritiesCommand(Filter).Process(this);
		}

		private void ExecutedFindCommand(object sender, ExecutedRoutedEventArgs e)
		{
			Lookup();
		}

		private void CanExecuteFindCommand(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = Filter != null;
		}

		private void ExecutedClearCommand(object sender, ExecutedRoutedEventArgs e)
		{
			Filter = new Security();
		}
	}
}
