import pandas as panda

# Load data
df = panda.read_csv('twine-mutation-dataset.csv')

# Constants
humanLap = 47.6

# Drop columns
df = df.drop(columns=[  'campaign_id',
                        'campaign_name',
                        'job_id',
                        'seed',
                        'num_envs',
                        'threshold_reached_at_step',
                        'training_duration_seconds',
                        'average_cpu_percent',
                        'peak_cpu_percent',
                        'average_ram_mib',
                        'peak_ram_mib',
                        'cpu_model',
                        'cpu_logical_cores',
                        'available_ram_mib',
                        'task_name',
                        'algorithm',
                        'max_steps',
                        'learning_rate',
                        'batch_size',
                        'buffer_size',
                        'hidden_units',
                        'num_layers',
                        'network_architecture',
                        'created_at',
                        'finished_at',
                        'completed_laps' ], errors = "ignore")

# Drop unfinished rows
df = df[df['status'] == "completed"]


"""
    Pick training instances with best lap time for final dataset

    1) Instantiate new dataframe with same (cleaned up) columns from original dataframe
    2) For every unique value in 'ordinal' column:
        - isolate the rows that contain the given value in 'ordinal'
        - choose the row with the highest value in 'fastest_lap_seconds'
        - add selected row to final dataset
"""
cleanedData = panda.DataFrame(columns = list(df))

for val in df['ordinal'].unique():
    subFrame = df[df['ordinal'] == val]
    locBestTime = subFrame['fastest_lap_seconds'].idxmin()
    cleanedData.loc[len(cleanedData)] = subFrame.loc[locBestTime]
    
cleanedData = cleanedData.drop(columns = ["status", "ordinal"], errors = "ignore")
threshold_reached_at_seconds = cleanedData.pop("threshold_reached_at_seconds")
cleanedData.insert(len(cleanedData.columns), "threshold_reached_at_seconds", threshold_reached_at_seconds)

# Change values in human_lap_target_seconds to be humanLap
cleanedData['human_lap_target_seconds'] = humanLap

# Finally, export dataframe as new CSV
cleanedData.to_csv('twine-mutation-dataset-clean.csv', index=False)