import pandas as pd
from sklearn.ensemble import RandomForestRegressor
from sklearn.metrics import r2_score
from sklearn.model_selection import train_test_split
from typing import Tuple

data = pd.read_csv("cleaned_data.csv")
targets = ["fastest_lap_seconds", "threshold_reached_at_seconds"]

Y = data[targets]
X = data.drop(columns = targets)

X_train, X_test, Y_train, Y_test = train_test_split(X, Y, test_size = 0.2, shuffle = True)
X_train, X_validation, Y_train, Y_validation = train_test_split(X_train, Y_train, test_size = 0.25, shuffle = True)

print("The data is now split into\n             X.shape  Y.shape")
print("Train:      ", X_train.shape, Y_train.shape)
print("Validation: ", X_validation.shape, Y_validation.shape)
print("Test:       ", X_test.shape, Y_test.shape)

forest = RandomForestRegressor(min_samples_leaf = 5, random_state = 67)
forest.fit(X = X_train, y = Y_train)

Y_hat_train = forest.predict(X_train)
Y_hat_held_out = forest.predict(X_validation)

train_accuracy = r2_score(y_true = Y_train, y_pred = Y_hat_train, multioutput = "raw_values")
held_out_accuracy = r2_score(y_true = Y_validation, y_pred = Y_hat_held_out, multioutput = "raw_values")

print("The accuracies are:")
print("Train:      ", train_accuracy)
print("Validation: ", held_out_accuracy)

sample = [
            [51,    # allowed_slip_angle
            2.0,    # allowed_yaw_rate
            87,     # checkpoint_count
            1.8,    # checkpoint_reward_per_lap
            0.037,  # excessive_yaw_penalty_per_second
            0.098,  # forward_progress_reward_per_second
            9.5,    # lap_time_reward_at_target
            4.5,    # lap_time_reward_scale
            1.75,   # max_wall_contact_seconds
            57,     # maximum_slip_angle
            0.029,  # slip_penalty_per_second
            0.005,  # target_progress_reward_scale
            0.04,   # time_penalty_per_second
            0.65,   # wall_impact_penalty
            0.15,   # wall_penalty_per_second
            1.6,    # wall_ray_angle_power
            37,     # wall_ray_center_distance
            2.7,    # wall_ray_center_power
            6,      # wall_ray_count
            5,      # wall_ray_side_distance
            47.6]   # human_lap_target_seconds
            #46.58  # fastest_lap_seconds
            #.....  # threshold_reached_at_seconds
        ]

columns = [
            "allowed_slip_angle",
            "allowed_yaw_rate",
            "checkpoint_count",
            "checkpoint_reward_per_lap",
            "excessive_yaw_penalty_per_second",
            "forward_progress_reward_per_second",
            "lap_time_reward_at_target",
            "lap_time_reward_scale",
            "max_wall_contact_seconds",
            "maximum_slip_angle",
            "slip_penalty_per_second",
            "target_progress_reward_scale",
            "time_penalty_per_second",
            "wall_impact_penalty",
            "wall_penalty_per_second",
            "wall_ray_angle_power",
            "wall_ray_center_distance",
            "wall_ray_center_power",
            "wall_ray_count",
            "wall_ray_side_distance",
            "human_lap_target_seconds"
        ]
        
df = pd.DataFrame(sample, columns = columns)
prediction = forest.predict(df)

print(prediction)