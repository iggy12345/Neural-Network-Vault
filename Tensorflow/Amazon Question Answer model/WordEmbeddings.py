import tensorflow as tf
import tensorflow_hub as hub
import collections
import numpy as np
import random
import math

embed = None

def UniversalEmbedding(x):
	global embed
	if(embed == None):
		print("Getting universal sentence encoder")
		embed = hub.Module("https://tfhub.dev/google/universal-sentence-encoder/2")
	return embed(tf.squeeze(tf.cast(x, tf.string)), signature="default", as_dict=True)["default"]

def GetEmbeddingSize():
	global embed
	if(embed == None):
		print("Getting universal sentence encoder")
		embed = hub.Module("https://tfhub.dev/google/universal-sentence-encoder/2")
	return embed.get_output_info_dict()['default'].get_shape()[1].value

# Repurposed from here https://github.com/tensorflow/tensorflow/blob/master/tensorflow/examples/tutorials/word2vec/word2vec_basic.py
	
def build_dataset(words, n_words):
	"""Process raw inputs into a dataset."""
	count = [['UNK', -1]]
	count.extend(collections.Counter(words).most_common(n_words - 1))
	dictionary = dict()
	for word, _ in count:
		dictionary[word] = len(dictionary)
	data = list()
	unk_count = 0
	for word in words:
		index = dictionary.get(word, 0)
		if index == 0:  # dictionary['UNK']
			unk_count += 1
		data.append(index)
	count[0][1] = unk_count
	reversed_dictionary = dict(zip(dictionary.values(), dictionary.keys()))
	return data, count, dictionary, reversed_dictionary
	
def generate_batch(batch_size, num_skips, skip_window, data):
	data_index = 0
	assert batch_size % num_skips == 0
	assert num_skips <= 2 * skip_window
	batch = np.ndarray(shape=(batch_size), dtype=np.int32)
	labels = np.ndarray(shape=(batch_size, 1), dtype=np.int32)
	span = 2 * skip_window + 1  # [ skip_window target skip_window ]
	buffer = collections.deque(maxlen=span)  # pylint: disable=redefined-builtin
	if data_index + span > len(data):
		data_index = 0
	buffer.extend(data[data_index:data_index + span])
	data_index += span
	for i in range(batch_size // num_skips):
		context_words = [w for w in range(span) if w != skip_window]
		words_to_use = random.sample(context_words, num_skips)
		for j, context_word in enumerate(words_to_use):
			batch[i * num_skips + j] = buffer[skip_window]
			labels[i * num_skips + j, 0] = buffer[context_word]
		if data_index == len(data):
			buffer.extend(data[0:span])
			data_index = span
		else:
			buffer.append(data[data_index])
			data_index += 1
	# Backtrack a little bit to avoid skipping words in the end of a batch
	data_index = (data_index + len(data) - span) % len(data)
	return batch, labels