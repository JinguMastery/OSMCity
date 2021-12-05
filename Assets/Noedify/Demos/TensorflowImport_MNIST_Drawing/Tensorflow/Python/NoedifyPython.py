

def ExportModel(model, filename):

    import os
    import numpy as np

    paramFile = open(filename,"w+")
    previousConvLayer = -1
    print(model.layers.__len__())
    for l in range(0,model.layers.__len__()):
        if (np.shape(model.layers[l].get_weights())!=(0,)):
            if (np.shape(np.shape(model.layers[l].get_weights()[0]))==(4,)): # convolutional
                no_filters_cur = np.shape(model.layers[l].get_weights()[0])[3]
                no_filters_prev = np.shape(model.layers[l].get_weights()[0])[2]
                
                print("Convolutional layer with {} filters.".format(no_filters_prev*no_filters_cur))
                weights = model.layers[l].get_weights()[0]; # get weights
                print(np.shape(weights))
                for f in range(0,no_filters_cur):
                    outString = ""
                    for c in range(0,np.shape(weights)[2]):
                        for j in range(0,np.shape(weights)[1]):
                            for i in range(0,np.shape(weights)[0]):
                                outString = outString + "{},".format(weights[j,i,c,f])
                    outString = outString[:-1]
                    paramFile.write(outString)
                    paramFile.write("\n")
                paramFile.write("*\n")
                biases = model.layers[l].get_weights()[1]; # get biases
                print(np.shape(biases))
                for j in range(0,np.shape(biases)[0]):
                    paramFile.write("{}\n".format(biases[j]))
                paramFile.write("***\n")
                previousConvLayer = l
            elif (np.shape(np.shape(model.layers[l].get_weights()[0]))==(2,)): # fully-connected
                weights = model.layers[l].get_weights()[0]; # get weights
                print(np.shape(weights))
                if (previousConvLayer==-1): # if previous layer is FC
                    for j in range(0,np.shape(weights)[1]):
                        for i in range(0,np.shape(weights)[0]-1):
                            paramFile.write("{},".format(weights[i,j]))
                        paramFile.write("{}".format(weights[np.shape(weights)[0]-1,j]))
                        paramFile.write("\n")
                else: # if previous layer is CV
                    print("fully connected layer {}, following conv. layer {}".format(l,previousConvLayer))
                    weights_prev_conv = model.layers[previousConvLayer].get_weights()[0]; # get weights
                    N_filters = np.shape(model.layers[previousConvLayer].get_weights()[0])[3]
                    filter_shape = ((model.layers[previousConvLayer].input_shape[1] - model.layers[previousConvLayer].kernel_size[0]) / model.layers[previousConvLayer].strides[0] + 1,(model.layers[previousConvLayer].input_shape[2] - model.layers[previousConvLayer].kernel_size[1]) / model.layers[previousConvLayer].strides[1] + 1)
                    filter_shape = np.ceil(filter_shape)
                    print("# filters: {}, filter shape: ({},{})".format(N_filters,filter_shape[0],filter_shape[1]))
                    for j in range(0,np.shape(weights)[1]):
                        outString = ""
                        for fy in range(0,int(filter_shape[1])):
                            for fx in range(0,int(filter_shape[0])):
                                for f in range(0,N_filters):
                                    hi = 1
                                    #print("filter {}, fx {}, fy {} : {}".format(f,fx,fy,fy*N_filters*int(filter_shape[0]) + fx*N_filters + f))
                                    #paramFile.write("{},".format(weights[fy*N_filters*int(filter_shape[0]) + fx*N_filters + f,j]))
                                    #print("weight no: {}, to node: {}".format(fy*N_filters*int(filter_shape[0]) + fx*N_filters + f,j))
                                    #outString = outString + "{},".format(weights[fy*N_filters*int(filter_shape[0]) + fx*N_filters + f,j])
                        for f in range(0,N_filters):
                            for fy in range(0,int(filter_shape[1])):
                                for fx in range(0,int(filter_shape[0])):
                                    index = fy*N_filters*int(filter_shape[0]) + fx*N_filters + f
                                    weight_val = weights[index,j]
                                    outString = outString + "{},".format(weight_val,j)
                                    
                        
                        outString = outString[:-1]
                        paramFile.write(outString)
                        paramFile.write("\n")
                previousConvLayer = -1
                    
                paramFile.write("*\n")
                biases = model.layers[l].get_weights()[1]; # get biases
                print(np.shape(biases))
                for j in range(0,np.shape(biases)[0]):
                    paramFile.write("{}\n".format(biases[j]))
                paramFile.write("***\n")    
    paramFile.close()