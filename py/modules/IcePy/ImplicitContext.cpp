// **********************************************************************
//
// Copyright (c) 2003-2007 ZeroC, Inc. All rights reserved.
//
// This copy of Ice is licensed to you under the terms described in the
// ICE_LICENSE file included in this distribution.
//
// **********************************************************************

#ifdef _WIN32
#   include <IceUtil/Config.h>
#endif
#include <ImplicitContext.h>
#include <ObjectAdapter.h>
#include <Proxy.h>
#include <Util.h>
#include <Ice/ImplicitContext.h>

using namespace std;
using namespace IcePy;

namespace IcePy
{

extern PyTypeObject ImplicitContextType;

struct ImplicitContextObject
{
    PyObject_HEAD
    Ice::ImplicitContextPtr* implicitContext;
};

}

#ifdef WIN32
extern "C"
#endif
static ImplicitContextObject*
implicitContextNew(PyObject* /*arg*/)
{
    ImplicitContextObject* self = PyObject_New(ImplicitContextObject, &ImplicitContextType);
    if (self == NULL)
    {
        return NULL;
    }
    self->implicitContext = 0;
    return self;
}

#ifdef WIN32
extern "C"
#endif
static void
implicitContextDealloc(ImplicitContextObject* self)
{
    delete self->implicitContext;
    PyObject_Del(self);
}

#ifdef WIN32
extern "C"
#endif
static int
implicitContextCompare(ImplicitContextObject* c1, ImplicitContextObject* c2)
{
    if(*c1->implicitContext < *c2->implicitContext)
    {
        return -1;
    }
    else if(*c1->implicitContext == *c2->implicitContext)
    {
        return 0;
    }
    else
    {
        return 1;
    }
}

#ifdef WIN32
extern "C"
#endif
static PyObject*
implicitContextGetContext(ImplicitContextObject* self)
{
    Ice::Context ctx = (*self->implicitContext)->getContext();

    PyObjectHandle dict = PyDict_New();
    if(dict.get() == NULL)
    {
        return NULL;
    }

    if(!contextToDictionary(ctx, dict.get()))
    {
        return NULL;
    }

    return dict.release();
}


#ifdef WIN32
extern "C"
#endif
static PyObject*
implicitContextSetContext(ImplicitContextObject* self, PyObject* args)
{
    PyObject* dict;
    if(!PyArg_ParseTuple(args, STRCAST("O!"), &PyDict_Type, &dict))
    {
        return NULL;
    }

    Ice::Context ctx;
    if(!dictionaryToContext(dict, ctx))
    {
        return NULL;
    }

    (*self->implicitContext)->setContext(ctx);

    Py_INCREF(Py_None);
    return Py_None;
}

#ifdef WIN32
extern "C"
#endif
static PyObject*
implicitContextContainsKey(ImplicitContextObject* self, PyObject* args)
{
    char* key;
    if(!PyArg_ParseTuple(args, STRCAST("s"), &key))
    {
        return NULL;
    }
    
    bool containsKey;
    try
    {
        containsKey = (*self->implicitContext)->containsKey(key);
    }
    catch(const Ice::Exception& ex)
    {
        setPythonException(ex);
        return NULL;
    }
    
    if(containsKey)
    {
        Py_INCREF(Py_True);
        return Py_True;
    }
    else
    {
        Py_INCREF(Py_False);
        return Py_False;
    }
}

#ifdef WIN32
extern "C"
#endif
static PyObject*
implicitContextGet(ImplicitContextObject* self, PyObject* args)
{
    char* key;
    if(!PyArg_ParseTuple(args, STRCAST("s"), &key))
    {
        return NULL;
    }
    
    string val;
    try
    {
        val = (*self->implicitContext)->get(key);
    }
    catch(const Ice::Exception& ex)
    {
        setPythonException(ex);
        return NULL;
    }
    return PyString_FromString(const_cast<char*>(val.c_str()));
}


#ifdef WIN32
extern "C"
#endif
static PyObject*
implicitContextPut(ImplicitContextObject* self, PyObject* args)
{
    char* key;
    char* val;
    if(!PyArg_ParseTuple(args, STRCAST("ss"), &key, &val))
    {
        return NULL;
    }
    
    string oldVal;
    try
    {
        (*self->implicitContext)->put(key, val);
    }
    catch(const Ice::Exception& ex)
    {
        setPythonException(ex);
        return NULL;
    }
    return PyString_FromString(const_cast<char*>(oldVal.c_str()));
}

#ifdef WIN32
extern "C"
#endif
static PyObject*
implicitContextRemove(ImplicitContextObject* self, PyObject* args)
{
    char* key;
    if(!PyArg_ParseTuple(args, STRCAST("s"), &key))
    {
        return NULL;
    }
    
    string val;
    try
    {
        val = (*self->implicitContext)->remove(key);
    }
    catch(const Ice::Exception& ex)
    {
        setPythonException(ex);
        return NULL;
    }
    return PyString_FromString(const_cast<char*>(val.c_str()));
}
    
static PyMethodDef ImplicitContextMethods[] =
{
    { STRCAST("getContext"), (PyCFunction)implicitContextGetContext, METH_VARARGS,
      PyDoc_STR(STRCAST("getContext() -> Ice.Context")) },
    { STRCAST("setContext"), (PyCFunction)implicitContextSetContext, METH_VARARGS,
      PyDoc_STR(STRCAST("setContext(ctx) -> string")) },
    { STRCAST("containsKey"), (PyCFunction)implicitContextContainsKey, METH_VARARGS,
      PyDoc_STR(STRCAST("containsKey(key) -> bool")) },
    { STRCAST("get"), (PyCFunction)implicitContextGet, METH_VARARGS,
      PyDoc_STR(STRCAST("get(key) -> string")) },
    { STRCAST("put"), (PyCFunction)implicitContextPut, METH_VARARGS,
      PyDoc_STR(STRCAST("put(key, value) -> string")) },
    { STRCAST("remove"), (PyCFunction)implicitContextRemove, METH_VARARGS,
      PyDoc_STR(STRCAST("remove(key) -> string")) },
    { NULL, NULL} /* sentinel */
};

namespace IcePy
{

PyTypeObject ImplicitContextType =
{
    /* The ob_type field must be initialized in the module init function
     * to be portable to Windows without using C++. */
    PyObject_HEAD_INIT(NULL)
    0,                              /* ob_size */
    STRCAST("IcePy.ImplicitContext"),    /* tp_name */
    sizeof(ImplicitContextObject),       /* tp_basicsize */
    0,                              /* tp_itemsize */
    /* methods */
    (destructor)implicitContextDealloc,  /* tp_dealloc */
    0,                              /* tp_print */
    0,                              /* tp_getattr */
    0,                              /* tp_setattr */
    (cmpfunc)implicitContextCompare,     /* tp_compare */
    0,                              /* tp_repr */
    0,                              /* tp_as_number */
    0,                              /* tp_as_sequence */
    0,                              /* tp_as_mapping */
    0,                              /* tp_hash */
    0,                              /* tp_call */
    0,                              /* tp_str */
    0,                              /* tp_getattro */
    0,                              /* tp_setattro */
    0,                              /* tp_as_buffer */
    Py_TPFLAGS_DEFAULT,             /* tp_flags */
    0,                              /* tp_doc */
    0,                              /* tp_traverse */
    0,                              /* tp_clear */
    0,                              /* tp_richcompare */
    0,                              /* tp_weaklistoffset */
    0,                              /* tp_iter */
    0,                              /* tp_iternext */
    ImplicitContextMethods,              /* tp_methods */
    0,                              /* tp_members */
    0,                              /* tp_getset */
    0,                              /* tp_base */
    0,                              /* tp_dict */
    0,                              /* tp_descr_get */
    0,                              /* tp_descr_set */
    0,                              /* tp_dictoffset */
    0,                              /* tp_init */
    0,                              /* tp_alloc */
    (newfunc)implicitContextNew,         /* tp_new */
    0,                              /* tp_free */
    0,                              /* tp_is_gc */
};

}

bool
IcePy::initImplicitContext(PyObject* module)
{
    if(PyType_Ready(&ImplicitContextType) < 0)
    {
        return false;
    }
    if(PyModule_AddObject(module, STRCAST("ImplicitContext"), (PyObject*)&ImplicitContextType) < 0)
    {
        return false;
    }

    return true;
}

PyObject*
IcePy::createImplicitContext(const Ice::ImplicitContextPtr& implicitContext)
{
    ImplicitContextObject* obj = implicitContextNew(NULL);
    if(obj != NULL)
    {
        obj->implicitContext = new Ice::ImplicitContextPtr(implicitContext);
    }
    return (PyObject*)obj;
}
