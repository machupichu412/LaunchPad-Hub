param env string
@minValue(1)
param amount int
param alertEmail string = ''
param startDate string = utcNow('yyyy-MM-01')

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: 'budget-launchpad-${env}'
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: startDate
      endDate: '2036-10-01'
    }
    notifications: empty(alertEmail) ? {} : {
      actual80: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [ alertEmail ]
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [ alertEmail ]
      }
    }
  }
}
