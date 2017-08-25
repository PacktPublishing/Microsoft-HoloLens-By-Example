
"""
https://azure.microsoft.com/en-us/try/cognitive-services/
https://azure.microsoft.com/en-gb/services/cognitive-services/face/
https://azure.microsoft.com/en-gb/try/cognitive-services/my-apis/

https://docs.microsoft.com/en-gb/azure/cognitive-services/face/quickstarts/python

"""

import sys, os, getopt, io, json
from PIL import Image
import cognitive_face as cf

def create_group(group_id):
    """ creates a new group 
    """
    try:
        res = cf.person_group.create(group_id, 'hololens_by_example_group', 'test group for sample application for Hololens by Example')
    except cf.CognitiveFaceException as cfe:
        print(cfe.msg)
        return -1 

    return 1

def create_persons(group_id, source_directory):
    print('creating persons in directory {}'.format(source_directory)) 

    persons = []  

    subdirs = [x[0] for x in os.walk(source_directory)] 
                                                                    
    for subdir, dirs, files in os.walk(source_directory):
        for dir in dirs:
            person = create_person(group_id, dir, os.path.join(source_directory, dir))
            if person and len(person['face_ids']) > 0:
                persons.append(person)

    return persons

def create_person(group_id, name, source_directory):
    print('creating person {} using images from directory {}'.format(name, source_directory)) 

    person = {}
    person['name'] = name
    person['person_id'] = '' 
    person['face_ids'] = []

    res = cf.person.create(group_id, name)

    if "personId" not in res:
        raise Exception('failed to create person {}'.format(name))        

    person_id = res['personId']

    person['person_id'] = person_id

    persisted_face_ids = {}

    for subdir, dirs, files in os.walk(source_directory):
        for file in files:
            if file.lower().endswith(".jpg") or file.lower().endswith(".jpeg") or file.lower().endswith(".png"):
                persisted_face_ids[os.path.join(subdir, file)] = ''

                res = cf.person.add_face(os.path.join(subdir, file), group_id, person_id, None, None)

                if 'persistedFaceId' not in res:
                    print('ERROR: failed to add face {} to {}'.format(os.path.join(subdir, file), name))
                else:
                    person['face_ids'].append(res['persistedFaceId']) 

    print('... added {} faces to {}'.format(len(person['face_ids']), name))

    return person 

def train_group(group_id):
    print("training {}".format(group_id))
    res = cf.person_group.train(person_group_id=group_id)

def export(group_id, persons, output_file): 

    json_obj = {
        'group_id': group_id, 
        'persons': persons
    }

    with open(output_file, 'w') as f:
        json.dump(json_obj, f, indent=4)

def test_persons(group_id, source_directory):
    print('testing persons in directory {}'.format(source_directory)) 

    subdirs = [x[0] for x in os.walk(source_directory)] 
                                                                    
    for subdir, dirs, files in os.walk(source_directory):
        for file in files:
            if file.lower().endswith(".jpg") or file.lower().endswith(".jpeg") or file.lower().endswith(".png"):
                img_filepath = os.path.join(subdir, file)
                res = cf.face.detect(
                    img_filepath, 
                    face_id=True, 
                    landmarks=False, 
                    attributes='')
            
                print('=========== results for {} ===========')
                print(res) 

                for recognized_face in res:
                    face_id = recognized_face['faceId']
                    identity_res = cf.face.identify(
                        face_ids=[face_id], 
                        person_group_id=group_id, 
                        max_candidates_return=1, 
                        threshold=None)

                    print(identity_res)

def main(argv):
    """
    Regions 
    West US - westus.api.cognitive.microsoft.com
    East US 2 - eastus2.api.cognitive.microsoft.com
    West Central US - westcentralus.api.cognitive.microsoft.com
    West Europe - westeurope.api.cognitive.microsoft.com
    Southeast Asia - southeastasia.api.cognitive.microsoft.com
    """
    subscription_key = ''
    group_id = ''
    source_directory = ''
    output_file = ''
    region = 'westcentralus'

    try:
        opts, args = getopt.getopt(argv,"hk:g:d:o:")
    except getopt.GetoptError:
        print('create_group.py -k <subscription_key> -g <group_id> -d <source_directory> -o <output_file> [-r <region>]') 
        sys.exit(2)
    
    for opt, arg in opts:
        if opt == '-h':
            print('create_group.py -k <subscription_key> -g <group_id> -d <source_directory> -o <output_file> [-r <region>]')
            print('\nStructure of source_directory; each person to have have their own directory')
            print('\nwith the name of the persons id. The contents is to include sample jpegs for training.')
            print('\nValid regions: westus, eastus2, westcentralus, westeurope, and southeastasia') 
            sys.exit()
        elif opt == "-k":
            subscription_key = arg
        elif opt == "-g":
            group_id = arg
        elif opt == "-d":
            source_directory = arg
        elif opt == "-o":
            output_file = arg
        elif opt == '-r':
            region = arg 

    if len(subscription_key) == 0 or len(group_id) == 0 or len(source_directory) == 0 or len(output_file) == 0:
        print('create_group.py -k <subscription_key> -g <group_id> -d <source_directory>') 
        sys.exit(2)

    cf.util._BASE_URL = "https://{}.api.cognitive.microsoft.com/face/v1.0/".format(region)

    cf.Key.set(subscription_key)

    create_group(group_id)

    persons = create_persons(group_id, source_directory)

    train_group(group_id)

    export(group_id, persons, output_file)

    test_persons(group_id, source_directory)

    sys.exit()

if __name__ == "__main__":
    main(sys.argv[1:])
    