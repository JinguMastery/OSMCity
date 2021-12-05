import tensorflow as tf
import tensorflow.keras as keras
from tensorflow.keras.datasets import mnist
from tensorflow.keras.models import Sequential
from tensorflow.keras.layers import Dense, Dropout, Activation, Flatten
from tensorflow.keras.callbacks import TensorBoard
import numpy as np

batch_size = 128
num_classes = 10

# Import Training Data
img_rows, img_cols = 28, 28

(x_train, y_train), (x_test, y_test) = mnist.load_data()

x_train = x_train.reshape(x_train.shape[0], img_rows*img_cols*1)
x_test = x_test.reshape(x_test.shape[0],img_rows*img_cols*1)

x_train = x_train.astype('float32')
x_test = x_test.astype('float32')
x_train /= 255
x_test /= 255

y_train = keras.utils.to_categorical(y_train, num_classes)
y_test = keras.utils.to_categorical(y_test, num_classes)


# Build Network
model = Sequential()
model.add(Dense(600, activation='sigmoid'))
model.add(Dense(300, activation='sigmoid'))
model.add(Dense(140, activation='sigmoid'))

model.add(Dense(num_classes, activation='sigmoid'))

model.compile(loss = "categorical_crossentropy", 
             optimizer="adam",
             metrics=['accuracy'])

# Train Network
epochs = 10
model.fit(x_train, y_train,
          batch_size=batch_size,
          epochs=epochs,
          verbose=1,
          validation_data=(x_test, y_test))

# Export Network Parameters
import NoedifyPython
NoedifyPython.ExportModel(model, "FC_mnist_600x300x140_parameters.txt")