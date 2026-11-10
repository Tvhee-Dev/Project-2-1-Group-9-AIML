from sklearn.model_selection import GroupKFold, GroupShuffleSplit, cross_validate
from sklearn.tree import DecisionTreeRegressor
from sklearn.ensemble import RandomForestRegressor
from pathlib import Path

import numpy as np
import pandas as pd


DATA_PATH = Path(__file__).parent / "data" / "runs.csv"
OUTPUT_PATH = Path(__file__).parent / "forest_predictions.csv"

cvSeed = 45

TARGET = "fastest_lap_seconds"

# normal runs learn to complate the lap in less than 100 seconds, on average it takes around 60s
# this var helps filter out outliers
MAX_LAP_SECONDS = 100

# every config is trained with several seeds and shares the same ordinal,
# splits keep all runs of one config on the same side so the model is tested on unseen configs
GROUP = "ordinal"

# the environment hyperparameters that vary between runs in runs.csv
# (ids, seed, constant columns and measured outcomes like completed_laps are left out)
features = [
    "allowed_slip_angle", "allowed_yaw_rate", "checkpoint_count", "checkpoint_reward_per_lap",
    "excessive_yaw_penalty_per_second", "forward_progress_reward_per_second",
    "lap_time_reward_at_target", "lap_time_reward_scale", "max_wall_contact_seconds",
    "maximum_slip_angle", "slip_penalty_per_second", "target_progress_reward_scale",
    "time_penalty_per_second", "wall_impact_penalty", "wall_penalty_per_second",
    "wall_ray_angle_power", "wall_ray_center_distance", "wall_ray_center_power",
    "wall_ray_count", "wall_ray_side_distance",
]


def K_Fold_CV(X, y, groups, max_depth, k=10):
    model = DecisionTreeRegressor(max_depth=max_depth, random_state=42)
    folds = GroupKFold(n_splits=k)

    scores = cross_validate(model, X, y, groups=groups, cv=folds, scoring="neg_mean_absolute_error", return_train_score=True)
   # scikit-learn gives the error as a negative number,
   # the minus sign makes it positive again
    validation_errors = -scores["test_score"]
    training_errors = -scores["train_score"]

    return np.mean(validation_errors), np.std(validation_errors), np.mean(training_errors)

def build_forest(X, y, n_trees, max_depth=None, seed=cvSeed):
    forest = RandomForestRegressor(n_estimators=n_trees, max_depth=max_depth, max_features=0.33, bootstrap=True, random_state=seed)
    forest.fit(X, y)
    return forest

def K_Fold_CV_forest(X, y, groups, n_trees, max_depth=None, k=10, seed=cvSeed):
    model = RandomForestRegressor(n_estimators=n_trees, max_depth=max_depth, max_features=0.33, random_state=seed, n_jobs=-1)
    folds = GroupKFold(n_splits=k)

    scores = cross_validate(model, X, y, groups=groups, cv=folds, scoring="neg_mean_absolute_error", return_train_score=True)
    validation_errors = -scores["test_score"]
    training_errors = -scores["train_score"]

    return np.mean(validation_errors), np.std(validation_errors), np.mean(training_errors)

def print_scores(name, scores):
    cv_mean, cv_std, train_mean = scores
    print(name, "CV mean absolute error:", cv_mean)
    print(name, "CV std:", cv_std)
    print(name, "train mean absolute error:", train_mean)

data = pd.read_csv(DATA_PATH)
# only finished runs that actually drove a lap have a lap time to learn from
data = data[data["status"] == "completed"].dropna(subset=[TARGET])
# filtering happens here, we compare the lap time from data to our pre-set time
data = data[data[TARGET] < MAX_LAP_SECONDS]

splitter = GroupShuffleSplit(n_splits=1, test_size=0.2, random_state=42)
train_idx, test_idx = next(splitter.split(data, groups=data[GROUP]))
train_data, test_data = data.iloc[train_idx], data.iloc[test_idx]

X_train = train_data[features].to_numpy()
y_train = train_data[TARGET].to_numpy()
groups_train = train_data[GROUP].to_numpy()
X_test = test_data[features].to_numpy()
y_test = test_data[TARGET].to_numpy()

print_scores("Tree", K_Fold_CV(X_train, y_train, groups_train, max_depth=5, k=10))
print_scores("Forest", K_Fold_CV_forest(X_train, y_train, groups_train, n_trees=50))

final_forest = build_forest(X_train, y_train, n_trees=300)
test_predictions = final_forest.predict(X_test)
print("Forest test mean absolute error:", np.mean(np.abs(test_predictions - y_test)))
# baseline: always guess the median lap time of the training runs, the forest should beat this
print("Baseline test mean absolute error:", np.mean(np.abs(np.median(y_train) - y_test)))

test_data = test_data.copy()
test_data["predicted_" + TARGET] = test_predictions
test_data.to_csv(OUTPUT_PATH, index=False)
print("Saved", OUTPUT_PATH.name, "with", len(test_data), "rows")
