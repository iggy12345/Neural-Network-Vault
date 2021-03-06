﻿using NeuralNetworkFundamentals.Activation_Functions;
using NeuralNetworkFundamentals.Activation_Functions.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Troschuetz.Random;
using System.Xml.Linq;
using System.Xml.XPath;

namespace NeuralNetworkFundamentals
{
    public class Neuron
    {
        // This class is designed around the neural network example used here:
        // https://mattmazur.com/2015/03/17/a-step-by-step-backpropagation-example/

        /*
         * The inputWeights are the weights between the each neuron in the last layer, and the current neuron
         * the inputNeurons are the list of the each neuron that is in the layer previous to this one and should be updated between trainings.
         * inputLayer is used if this neuron is on the input layer
         * inputs is the array used if the neuron is on the input layer
         * inputs_collected determines when each neuron in the previous has fired and this neuron has received their outputs
         */

        #region Delegates
        /// <summary>
        /// Used for the activation event
        /// </summary>
        /// <param name="sender">neuron sending the event</param>
        /// <param name="e">activation event arguments</param>
        public delegate void ActivationEventHandler(object sender, ActivationEventArgs e);
        #endregion

        #region Events
        /// <summary>
        /// Triggered each time that the neuron activates (Allows its output links to update their activation input values)
        /// </summary>
        public event ActivationEventHandler ActiveEvent;    // Triggered when this Neuron finishes calculating its activation value
        #endregion

        #region Properties
        private double activation;          // This represents how activated the neuron is
        private List<double> weights;       // Weight to be passed to the next neuron
        private List<double> prevWeights;   // Weights of the current forward propagation but un-updated from back propagation
        private double net;                 // The net output of the neuron without being activated
        private double bias;                // The bias of the neuron
        private double delta;               // Contains the delta of the neuron
        private double prevDelta;           // previous delta, used for momentum

        private bool inputLayer;            // Determines if the inputs to this neuron will be in the form of Neurons, or doubles
        private bool outputLayer;           // Determines if the outputs of this neuron are the final stage of the network, determines what kind of backpropagation to use

        /// <summary>
        /// List of neurons that feed into this neuron, used for subscribing and storing activation values
        /// </summary>
        private List<long> inputNeurons;    // List of IDs of the neurons that input into this neuron
        private List<double> inputs;        // Inputs into the Neuron if inputLayer is false

        /// <summary>
        /// List of neurons that are linked to the output of this neuron, used for subscribing and back propagation
        /// </summary>
        private List<int> outputNeurons;    // Contains the indices of this neuron's weights in the next layer of neurons.

        private double rawInput;            // Inputs into the Neuron if inputLayer is true

        /// <summary>
        /// Used to determine when all of the inputs from each input neuron has been collected
        /// </summary>
        private bool[] inputs_collected;    // Specifies whether the inputs for the Neuron have been collected or not

        /// <summary>
        /// Used to launch a separate thread for activation
        /// </summary>
        private Thread activationThread;    // Used to launch the activation event in a new thread as an asynchronous process.

        private ActivationFunction defaultActivation;
        private ActivationParameters defaultParameters;

        private long id;                    // The unique Identifier that tells this Neuron apart form every other Neuron
        private static long NeuronCount;    // How many Neurons currently exist
        #endregion

        #region Accessor Methods

        /// <summary>
        /// Raw input to the neuron, only used if it's an input neuron to the network
        /// </summary>
        public double RawInput { get => rawInput; set => rawInput = value; }            // Sets the inputs to the Neuron

        /// <summary>
        /// Sets the current weight values for the neuron
        /// Size of the List must match the number of neurons that this neuron is subscribed to.
        /// </summary>
        public List<double> Weights { get => weights; set => weights = value; }   // Sets the initial weight value for the Neuron

        /// <summary>
        /// Current output level of the neuron
        /// </summary>
        public double Activation { get => activation; set => activation = value; }  // The output of the Neuron

        /// <summary>
        /// The default activation function for this neuron, is used when null is passed into the activate function
        /// </summary>
        public ActivationFunction DefaultActivation { get => defaultActivation; set => defaultActivation = value; }   // returns the default activation function class instance

        /// <summary>
        /// The default activation function parameters, is not used most of the time except for functions requiring alpha/beta stuff
        /// </summary>
        public ActivationParameters DefaultParameters { get => defaultParameters; set => defaultParameters = value; }

        /// <summary>
        /// The net value of the neuron (all of the inputs multiplied by their weights plus the bias)
        /// </summary>
        public double Net { get => net; set => net = value; }

        /// <summary>
        /// The unique id number assigned to this neuron at initialization
        /// </summary>
        public long ID { get => id; set => id = value; }

        /// <summary>
        /// The current bias of the neuron
        /// </summary>
        public double Bias { get => bias; set => bias = value; }

        /// <summary>
        /// The threshold of the neuron (-bias)
        /// </summary>
        public double Threshold { get => -bias; set => bias = -value; }

        /// <summary>
        /// The current delta for the neuron
        /// </summary>
        public double Delta { get => delta; set => delta = value; }

        /// <summary>
        /// The previous weights of the neuron (will be empty if it has never been used)
        /// </summary>
        public List<double> PrevWeights { get => prevWeights; set => prevWeights = value; }

        /// <summary>
        /// The current number of neurons that exist
        /// </summary>
        public static long Count { get => NeuronCount; }

        /// <summary>
        /// Flags whether this neuron is part of the input layer
        /// </summary>
        public bool InputLayer { get => inputLayer; }

        /// <summary>
        /// Flags whether this neuron is part of the output layer
        /// </summary>
        public bool OutputLayer { get => outputLayer; }

        /// <summary>
        /// The previous delta of the neuron (will be zero if never been used)
        /// </summary>
        public double PrevDelta { get => prevDelta; }

        /// <summary>
        /// The current inputs into the neuron, from other neurons
        /// </summary>
        public List<double> InputValues { get => inputs; set => inputs = value; }

        #endregion

        #region Constructors
        // These all call the Setup Functions
        /// <summary>
        /// Constructor for the neuron class
        /// </summary>
        /// <param name="inputNeurons">List of neurons that feed into the neuron</param>
        /// <param name="weight">List of weights to be used on the connections to the input neurons (optional, list of zeros by default)</param>
        /// <param name="bias">Initial bias for the neuron (0)</param>
        /// <param name="outputLayer">Flags whether the neuron is in the output layer (false)</param>
        /// <param name="defaultActivation">default activation function to use (Sigmoid)</param>
        /// <param name="defaultParameters">default activation parameters to use (Sigmoid, empty)</param>
        public Neuron(Neuron[] inputNeurons, List<double> weight = null, double bias = 0, bool outputLayer = false,
            ActivationFunction defaultActivation = null, ActivationParameters defaultParameters = null)
        {
            // Creates a new neuron and links it to all of it's input Neurons
            Setup(inputNeurons.ToList(), weight, bias, outputLayer, defaultActivation, defaultParameters);
        }

        /// <summary>
        /// Constructor for the neuron class
        /// </summary>
        /// <param name="inputNeurons">List of neurons that feed into the neuron</param>
        /// <param name="weight">List of weights to be used on the connections to the input neurons (optional, list of zeros by default)</param>
        /// <param name="bias">Initial bias for the neuron (0)</param>
        /// <param name="outputLayer">Flags whether the neuron is in the output layer (false)</param>
        /// <param name="defaultActivation">default activation function to use (Sigmoid)</param>
        /// <param name="defaultParameters">default activation parameters to use (Sigmoid, empty)</param>
        public Neuron(List<Neuron> inputNeurons, List<double> weight = null, double bias = 0, bool outputLayer = false,
            ActivationFunction defaultActivation = null, ActivationParameters defaultParameters = null)
        {
            // Creates a new neuron and links it to all of it's input Neurons
            Setup(inputNeurons, weight, bias, outputLayer, defaultActivation, defaultParameters);
        }

        /// <summary>
        /// Constructor for the neuron class (input layer version)
        /// </summary>
        /// <param name="bias">Initial bias for the neuron (0)</param>
        /// <param name="defaultActivation">default activation function to use (Sigmoid)</param>
        /// <param name="defaultParameters">default activation parameters to use (Sigmoid, empty)</param>
        /// <param name="inputLayer">Flags whether the neuron is in the input layer (true)</param>
        public Neuron(double bias = 0,
            ActivationFunction defaultActivation = null, ActivationParameters defaultParameters = null, bool inputLayer = true)
        {
            // Specifies that this neuron is an input neuron
            Setup(bias, defaultActivation, defaultParameters, inputLayer);
        }

        // Performs the actual construction
        /// <summary>
        /// Sets up the physical neuron (input layer version)
        /// </summary>
        /// <param name="bias">bias for the neuron (0)</param>
        /// <param name="defaultActivation">default activation function to use (Sigmoid)</param>
        /// <param name="defaultParameters">default activation parameters to use (Sigmoid, empty)</param>
        /// <param name="inputLayer">Flags whether the neuron is in the input layer (true)</param>
        private void Setup(double bias = 0,
            ActivationFunction defaultActivation = null, ActivationParameters defaultParameters = null, bool inputLayer = true)
        {
            activationThread = new Thread(OnActivation);    // Used to make the activation process asynchronous

            // Specifies that this neuron is an input neuron
            this.inputLayer = inputLayer;

            this.bias = bias;

            delta = 0;  // initializes delta value
            prevDelta = 0;

            this.defaultActivation = defaultActivation ?? new Sigmoid(); // default activation function
            this.defaultParameters = defaultParameters ?? new SigmoidParams(); // default activation parameters

            id = NeuronCount++;                         // assigns the Neuron ID and increments the count

            rawInput = 0;
        }

        /// <summary>
        /// Sets up the physcical neuron
        /// </summary>
        /// <param name="inputNeurons">List of neurons that feed into the neuron</param>
        /// <param name="weight">List of weights to be used on the connections to the input neurons (optional, list of zeros by default)</param>
        /// <param name="bias">Initial bias for the neuron (0)</param>
        /// <param name="outputLayer">Flags whether the neuron is in the output layer (false)</param>
        /// <param name="defaultActivation">default activation function to use (Sigmoid)</param>
        /// <param name="defaultParameters">default activation parameters to use (Sigmoid, empty)</param>
        private void Setup(List<Neuron> inputNeurons, List<double> weight = null, double bias = 0, bool outputLayer = false,
            ActivationFunction defaultActivation = null, ActivationParameters defaultParameters = null)
        {
            // Creates a new neuron and links it to all of it's input Neurons
            this.outputLayer = outputLayer;

            weights = weight ?? new List<double>(inputNeurons.Count());   // initial weight value
            PrevWeights = new List<double>(inputNeurons.Count);
            if (weight == null)
            {
                for (int i = 0; i < inputNeurons.Count; i++)
                {
                    weights.Add(0);
                }
            }
            for (int i = 0; i < inputNeurons.Count; i++)
            {
                PrevWeights.Add(0);
            }

            // Sets up the calling map for activating neurons and connecting with the previous layer
            this.inputNeurons = new List<long>(inputNeurons.Count);
            inputs = new List<double>(inputNeurons.Count);
            inputs_collected = new bool[inputNeurons.Count];

            for (int i = 0; i < inputs_collected.Length; i++)
            {
                inputs.Add(0);
                this.inputNeurons.Add(inputNeurons[i].ID);
                inputs_collected[i] = false;
            }

            Setup(bias, defaultActivation, defaultParameters, InputLayer);

            for (int i = 0; i < inputNeurons.Count; i++)
            {
                inputNeurons[i].ActiveEvent += OnActivate;  // Subscribes to the input Neuron's activation events
            }
        }
        #endregion

        #region Methods

        #region Subscription methods

        /// <summary>
        /// Subscribes this neuron to another neuron's activation event
        /// </summary>
        /// <param name="neuron">Neuron to subscribe to</param>
        public void SubscribeToActivation(Neuron neuron)
        {
            // Call to have the current neuron subscribe to a neuron that's passed in.
            neuron.ActiveEvent += OnActivate;

            // Sets up the input collection arrays and subscription lists.
            List<bool> tempCollected = inputs_collected.ToList();
            tempCollected.Add(false);
            inputs_collected = tempCollected.ToArray();
            weights.Add(0);
            prevWeights.Add(0);

            inputs.Add(0);
            inputNeurons.Add(neuron.ID);
        }

        /// <summary>
        /// Desubscribes a neuron from the activation event of another neuron
        /// </summary>
        /// <param name="neuron">Neuron to desubscribe from</param>
        /// <returns>Returns whether the neuron was already subscribed to, or not</returns>
        public bool DeSubscribeFromActivation(Neuron neuron)
        {
            // Call to have the current neuron desubscribe from the neuron that's passed in.
            // Returns a boolean representing whether the neuron was subscribed to that neuron, or not.
            if (inputNeurons.Contains(neuron.ID))
            {
                // THIS. IS. ABSURD!!!
                long tempLong = neuron.ID;  // Fuck ref and anonymous values in lambda expressions............ -_________________-
                Predicate<long> idFinder = delegate (long l) { return l == tempLong; };   // Why does this even exist, wtf???
                int index = inputNeurons.FindIndex(idFinder);

                neuron.ActiveEvent -= OnActivate;
                inputNeurons.Remove(neuron.ID);

                // Removes the subscription from the list of inputs, collection array, etc...
                List<bool> tempCollected = inputs_collected.ToList();
                tempCollected.RemoveAt(index);
                inputs_collected = tempCollected.ToArray();
                inputs.RemoveAt(index);
                weights.RemoveAt(index);
                prevWeights.RemoveAt(index);

                return true;
            }
            return false;
        }
        #endregion

        #region Activation methods

        /// <summary>
        /// Called when a neuron that is subscribed to triggers it's activation event, updates the activation value and flips the input collected flag to true
        /// </summary>
        /// <param name="sender">neuron triggering its event</param>
        /// <param name="e">activation arguments</param>
        public void OnActivate(object sender, ActivationEventArgs e)
        {
            // Figures out which Neuron fired this event, and then collects it's data.
            Neuron Sender = (Neuron)sender;
            if (inputNeurons.Contains(Sender.ID))
            {
                for (int i = 0; i < inputNeurons.Count; i++)
                {
                    if (Sender.id == inputNeurons[i])
                    {
                        // once the input Neuron's index is found, flag the boolean that corresponds to it and update it's value in the list
                        inputs_collected[i] = true;
                        inputs[i] = Sender.Activation;
                    }
                }
                bool temp = true;
                foreach (bool item in inputs_collected)
                {
                    if (!item)
                    {
                        temp = false;
                        break;
                    }
                }
                if (temp)
                    Activate();
            }
        }

        /// <summary>
        /// Activate function for this neuron multiplies all of the inputs times their weights, adds the bias, and activates the net value, then triggers its activation event
        /// </summary>
        /// <param name="type">Activation function to use (default activation function, from constructor)</param>
        /// <param name="Params">Activation function parameters to use (default activation parameters, from contructor)</param>
        public virtual void Activate(ActivationFunction type = null, ActivationParameters Params = null)
        {
            // These are the various activation functions that I could find on wikipedia:
            // https://en.wikipedia.org/wiki/Activation_function

            // This function doesn't provide functionality for Softmax, or Maxout, obviously

            type = type ?? DefaultActivation;
            Params = Params ?? DefaultParameters;

            if (!InputLayer)
            {
                // Input layers don't have weights and activation functions, that's why they get an exclusive case
                Net = bias;

                for (int i = 0; i < (inputNeurons.Count); i++)
                    Net += (inputs[i]) * (weights[i]);

                // Resets the inputs_collected list
                for (int i = 0; i < inputs_collected.Length; i++)
                    inputs_collected[i] = false;

                activation = type.Activate(Net, Params);
            }
            else
            {
                Net = rawInput;
                activation = net;
            }

            //Task.Factory.StartNew(OnActivation);
            OnActivation();

            //return Activation;
        }

        /// <summary>
        /// Calls the active event
        /// </summary>
        /// <param name="e">Activation Arguments to use</param>
        protected virtual void OnActiveEvent(ActivationEventArgs e)
        {
            ActiveEvent?.Invoke(this, e);
        }

        /// <summary>
        /// A helper function used to call the OnActiveEvent event that requires no arguments
        /// </summary>
        public void OnActivation()
        {
            // A helper function used to call the OnActiveEvent event that requires no arguments.
            OnActiveEvent(new ActivationEventArgs(activation, id, (InputLayer) ? rawInput : net));
        }
        #endregion

        #region weight and bias initialization methods

        /// <summary>
        /// Randomizes the weights using a normal distribution
        /// </summary>
        /// <param name="rnd">Random instance using trochuetz NormalDistribution class</param>
        public void RandomizeWeights(NormalDistribution rnd)
        {
            // Randomizes the weights according to the random generator sent in.
            if (!InputLayer)
            {
                for (int i = 0; i < weights.Count; i++)
                {
                    weights[i] = rnd.NextDouble();
                }
            }
        }

        /// <summary>
        /// Randomizes the weights using a provided random instance
        /// </summary>
        /// <param name="rnd">Random instance</param>
        public void RandomizeWeights(Random rnd)
        {
            // Randomizes the weights according to the random generator sent in.
            if (!InputLayer)
            {
                for (int i = 0; i < weights.Count; i++)
                {
                    weights[i] = rnd.NextDouble();
                }
            }
        }

        /// <summary>
        /// Randomizes the bias using a binomial distribution
        /// </summary>
        /// <param name="rnd">Random instance from Troschuetz Binomial Distribution class</param>
        public void RandomizeBias(BinomialDistribution rnd)
        {
            // Randomizes the bias according to the random number generator sent in.
            bias = rnd.NextDouble();
        }

        /// <summary>
        /// Randomizes the bias using a random instance
        /// </summary>
        /// <param name="rnd">Random instance</param>
        public void RandomizeBias(Random rnd)
        {
            // Randomizes the bias according to the random number generator sent in.
            bias = rnd.NextDouble();
        }
        #endregion

        #region delta methods

        /// <summary>
        /// Uses the assigned delta to adjust the weights for this neuron
        /// </summary>
        /// <param name="momentum">Momentum for this neuron (0)</param>
        /// <param name="learningRate">Learning rate for this neuron (1)</param>
        /// <param name="ExpectedOutput">Expected output for output layer neurons (0)</param>
        /// <param name="nextLayerNeurons">next layer neurons for not output layer neurons (null)</param>
        public void AdjustValues(double momentum = 0, double learningRate = 1, double ExpectedOutput = 0, List<Neuron> nextLayerNeurons = null)
        {
            // Backpropagates the values of the weights and biases based on the delta of this neuron
            if (!InputLayer)
            {
                for (int i = 0; i < weights.Count; i++)
                {
                    PrevWeights[i] = weights[i];
                    weights[i] += momentum * PrevDelta + learningRate * delta * inputs[i];
                }
            }
            bias += momentum * PrevDelta + learningRate * delta;
        }

        /// <summary>
        /// Assigns the delta to this neuron
        /// </summary>
        /// <param name="momentum">Momentum for this neuron (0)</param>
        /// <param name="learningRate">Learning rate for this neuron (1)</param>
        /// <param name="ExpectedOutput">Expected output for output layer neurons (0)</param>
        /// <param name="nextLayerNeurons">next layer of neurons for not output layer neurons (null)</param>
        /// <param name="AdjustValues">Automatically adjust the values of the weights using th calculated delta? (true)</param>
        /// <returns></returns>
        public double AssignDelta(double momentum = 0, double learningRate = 1, double ExpectedOutput = 0, List<Neuron> nextLayerNeurons = null, bool AdjustValues = true)
        {
            // Calculates the delta for the neuron and updates the neuron's value.
            prevDelta = delta;
            delta = defaultActivation.Derivate(net, defaultParameters);
            if (nextLayerNeurons == null)
            {
                // Performs delta calculation for output neurons
                if (OutputLayer)
                {
                    delta *= (ExpectedOutput - activation);
                }
                else
                    throw new InvalidOperationException("Invalid Neuron type!",
                        new Exception("Cannot calculate delta of non-output layer neuron without the next layer's neurons."));
            }
            else
            {
                // Performs delta calculation for non-output neurons
                if (outputNeurons == null)
                    PopulateOutputIndices(nextLayerNeurons);

                double sum = 0;
                for (int i = 0; i < nextLayerNeurons.Count; i++)
                {
                    sum += nextLayerNeurons[i].weights[outputNeurons[i]] * nextLayerNeurons[i].Delta;
                }
                delta *= sum;
            }

            void PopulateOutputIndices(List<Neuron> nextLayer)
            {
                outputNeurons = new List<int>(nextLayer.Count);
                for (int i = 0; i < nextLayer.Count; i++)
                {
                    if (nextLayer[i].inputNeurons.Contains(id))
                        for (int j = 0; j < nextLayer[i].inputNeurons.Count; j++)
                        {
                            if (nextLayer[i].inputNeurons[j] == id)
                            {
                                outputNeurons.Add(j);
                            }
                        }
                    else
                        throw new InvalidOperationException("Neuron not linked!",
                            new Exception("Cannot find this neuron's id in the next layer's neurons"));
                }
            }

            if (AdjustValues)
                this.AdjustValues(momentum, learningRate, ExpectedOutput, nextLayerNeurons);

            return delta;
        }

        #endregion

        #region File Methods
        /// <summary>
        /// Converts this neuron into its xml schema
        /// </summary>
        /// <returns>Returns the xml equivalent of this neuron</returns>
        public virtual XElement SerializeXml()
        {
            // Returns an Xelement that is writable to an xml file with all of the data that this neuron needs in order to be read.

            XElement temp = new XElement("Neuron",
                new XAttribute("Input", inputLayer),
                new XAttribute("Output", outputLayer),
                new XElement("Bias", bias),
                new XElement("PreviousDelta", prevDelta));

            if (!inputLayer)
            {
                XElement weightTemp = new XElement("Weights");
                for (int i = 0; i < Weights.Count; i++)
                    weightTemp.Add(new XElement("Weight",
                        new XAttribute("Index", i), weights[i]));

                temp.Add(weightTemp);

                XElement prevWeightTemp = new XElement("PreviousWeights");
                for (int i = 0; i < prevWeights.Count; i++)
                    prevWeightTemp.Add(new XElement("Weight",
                        new XAttribute("Index", i), prevWeights[i]));

                temp.Add(prevWeightTemp);
            }

            return temp;
        }

        /// <summary>
        /// Loads this neuron from its xml schema
        /// </summary>
        /// <param name="element">XElement to load from</param>
        public virtual void InitializeFromXml(XElement element)
        {
            // Reads data from an Xml element passed in, to initialize all of the values in the neuron.

            inputLayer = Convert.ToBoolean(element.Attribute("Input").Value);
            outputLayer = Convert.ToBoolean(element.Attribute("Output").Value);

            bias = Convert.ToDouble(element.XPathSelectElement("Bias").Value);
            prevDelta = Convert.ToDouble(element.XPathSelectElement("PreviousDelta").Value);

            // Handles current weights
            if (!inputLayer)
            {
                List<double> temp = new List<double>();
                int i = 0;
                while (element.XPathSelectElement("Weights").XPathSelectElement("Weight[@Index=" + i + "]") != null)
                {
                    temp.Add(Convert.ToDouble(element.XPathSelectElement("Weights").XPathSelectElement("Weight[@Index=" + (i++) + "]").Value));
                }
                weights = temp;

                // Handles previous Weights
                temp = new List<double>(weights.Count);
                i = 0;
                while (element.XPathSelectElement("PreviousWeights").XPathSelectElement("Weight[@Index=" + i + "]") != null)
                {
                    temp.Add(Convert.ToDouble(element.XPathSelectElement("PreviousWeights").XPathSelectElement("Weight[@Index=" + (i++) + "]").Value));
                }
                prevWeights = temp;
            }
        }

        // Static Methods for saving and loading from files, calls the overridable methods.
        /// <summary>
        /// Saves a neuron into its xml schema without having to call the neuron's method
        /// </summary>
        /// <param name="neuron">neuron to save</param>
        /// <returns>Returns the xml equivalent of the supplied neuron</returns>
        public static XElement Save(Neuron neuron)
        {
            return neuron.SerializeXml();
        }

        /// <summary>
        /// Loads a neuron from its xml schema without having to initialize a new neuron first
        /// </summary>
        /// <param name="element">XElement to load from</param>
        /// <returns>The neuron contained in the element</returns>
        public static Neuron Load(XElement element)
        {
            Neuron temp = new Neuron();
            temp.InitializeFromXml(element);
            return temp;
        }
        #endregion

        #endregion

        #region Event args
        /// <summary>
        /// Class for activation event
        /// </summary>
        public class ActivationEventArgs : EventArgs
        {
            /// <summary>
            /// Activation of the sending neuron
            /// </summary>
            public double Activation { get; set; }

            /// <summary>
            /// ID of the sending neuron
            /// </summary>
            public long ID { get; set; }

            /// <summary>
            /// input from the sending neuron
            /// </summary>
            public double Input { get; set; }

            public ActivationEventArgs(double activation, long ID, double Input)
            {
                Activation = activation;
                this.ID = ID;
                this.Input = Input;
            }
        }
        #endregion
    }
}
