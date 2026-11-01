from sklearn.model_selection import KFold, cross_validate, train_test_split
from sklearn.tree import DecisionTreeRegressor
from sklearn.ensemble import RandomForestRegressor
from pathlib import Path

import numpy as np
import pandas as pd


DATA_PATH = Path(__file__).parent / "data" / "runs.csv"
OUTPUT_PATH = Path(__file__).parent / "forest_predictions.csv"


#TODO
# fill in these with the appropriate attributes
# also if target is a number on a continous scale, then it is a regression problem 
# terefore we use Regressor instead of classifier
TARGET = ""
features = []


def K_Fold_CV(X, y, max_depth, k=10, seed=42):
    model = DecisionTreeRegressor(max_depth=max_depth, random_state=42)
    folds = KFold(n_splits=k, shuffle=True, random_state=seed)

    scores = cross_validate(model, X, y, cv=folds, scoring="neg_mean_absolute_error", return_train_score=True)  
   # scikit-learn gives the error as a negative number,
   # the minus sign makes it positive again
    validation_errors = -scores["test_score"]
    training_errors = -scores["train_score"]

    return np.mean(validation_errors), np.std(validation_errors), np.mean(training_errors)

def build_forest(X, y, n_trees, max_depth=None, seed=42):
    forest = RandomForestRegressor(n_estimators=n_trees, max_depth=max_depth, max_features=0.33, bootstrap=True, random_state=seed)
    forest.fit(X, y)
    return forest

def K_Fold_CV_forest(X, y, n_trees, max_depth=None, k=10, seed=42):
    model = RandomForestRegressor(n_estimators=n_trees, max_depth=max_depth, max_features=0.33, random_state=seed, n_jobs=-1)
    folds = KFold(n_splits=k, shuffle=True, random_state=seed)

    scores = cross_validate(model, X, y, cv=folds, scoring="neg_mean_absolute_error", return_train_score=True)
    validation_errors = -scores["test_score"]
    training_errors = -scores["train_score"]

    return np.mean(validation_errors), np.std(validation_errors), np.mean(training_errors)

def print_scores(name, scores):
    cv_mean, cv_std, train_mean = scores
    print(name, "CV mean absolute error:", cv_mean)
    print(name, "CV std:", cv_std)
    print(name, "train mean absolute error:", train_mean)

data = pd.read_csv(DATA_PATH)

train_data, test_data = train_test_split(data, test_size=0.2, random_state = 42)

X_train = train_data[features].to_numpy()
y_train = train_data[TARGET].to_numpy()
X_test = test_data[features].to_numpy()
y_test = test_data[TARGET].to_numpy()

print_scores("Tree", K_Fold_CV(X_train, y_train, max_depth=11, k=10))
print_scores("Forest", K_Fold_CV_forest(X_train, y_train, n_trees=50))

final_forest = build_forest(X_train, y_train, n_trees=50)
test_predictions = final_forest.predict(X_test)
print("Forest test mean absolute error:", np.mean(np.abs(test_predictions - y_test)))

test_data = test_data.copy()
test_data["predicted_" + TARGET] = test_predictions
test_data.to_csv(OUTPUT_PATH, index=False)
print("Saved", OUTPUT_PATH.name, "with", len(test_data), "rows")
