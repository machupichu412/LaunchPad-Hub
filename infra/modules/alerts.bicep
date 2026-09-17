@description('Environment name, e.g. dev, test, prod')
param env string

@description('Email address that receives alert notifications. Empty disables the action group and every alert with it — non-prod environments do not need to page anyone.')
param alertEmail string = ''

@description('Resource ids the alerts are scoped to')
param appServiceId string
param sqlDatabaseId string
param serviceBusNamespaceId string

@description('Failed-request threshold over a 5 minute window')
param http5xxThreshold int = 10

@description('Average response time threshold in seconds')
param responseTimeThresholdSeconds int = 2

@description('SQL CPU percentage threshold')
param sqlCpuThresholdPercent int = 80

var alertsEnabled = !empty(alertEmail)

// The four alerts below are the set launchpad-build-guide.md §11 already specifies. They
// existed only as a table in that document until now — nothing was deployed, so a 5xx
// spike, a stalled queue, or a saturated database produced no notification at all.
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (alertsEnabled) {
  name: 'ag-launchpad-${env}'
  location: 'global'
  properties: {
    groupShortName: 'lp${env}'
    enabled: true
    emailReceivers: [
      {
        name: 'oncall'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

resource http5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alertsEnabled) {
  name: 'alert-launchpad-${env}-http5xx'
  location: 'global'
  properties: {
    description: 'API is returning server errors. Check Application Insights failures, filtered by the correlation id on the responses.'
    severity: 1
    enabled: true
    scopes: [appServiceId]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'Http5xx'
          metricName: 'Http5xx'
          metricNamespace: 'Microsoft.Web/sites'
          operator: 'GreaterThan'
          threshold: http5xxThreshold
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [{ actionGroupId: actionGroup.id }]
  }
}

resource responseTimeAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alertsEnabled) {
  name: 'alert-launchpad-${env}-response-time'
  location: 'global'
  properties: {
    description: 'API responses are slow. Likely causes: SQL resumed from auto-pause, a missing index, or an unbounded list endpoint on a grown cohort.'
    severity: 2
    enabled: true
    scopes: [appServiceId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HttpResponseTime'
          metricName: 'HttpResponseTime'
          metricNamespace: 'Microsoft.Web/sites'
          operator: 'GreaterThan'
          threshold: responseTimeThresholdSeconds
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [{ actionGroupId: actionGroup.id }]
  }
}

// Any dead letter means a job was lost: a notification never sent, a matching run never
// completed, a SharePoint folder never provisioned. Threshold is zero deliberately.
resource deadLetterAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alertsEnabled) {
  name: 'alert-launchpad-${env}-deadletter'
  location: 'global'
  properties: {
    description: 'A Service Bus message dead-lettered. The work in it did not happen; inspect the dead-letter queue before purging.'
    severity: 1
    enabled: true
    scopes: [serviceBusNamespaceId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'DeadletteredMessages'
          metricName: 'DeadletteredMessages'
          metricNamespace: 'Microsoft.ServiceBus/namespaces'
          operator: 'GreaterThan'
          threshold: 0
          timeAggregation: 'Maximum'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [{ actionGroupId: actionGroup.id }]
  }
}

resource sqlCpuAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alertsEnabled) {
  name: 'alert-launchpad-${env}-sql-cpu'
  location: 'global'
  properties: {
    description: 'Azure SQL CPU is saturated. On a serverless database this also means it is billing at its ceiling.'
    severity: 2
    enabled: true
    scopes: [sqlDatabaseId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'cpu_percent'
          metricName: 'cpu_percent'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          operator: 'GreaterThan'
          threshold: sqlCpuThresholdPercent
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [{ actionGroupId: actionGroup.id }]
  }
}

output actionGroupId string = alertsEnabled ? actionGroup.id : ''
