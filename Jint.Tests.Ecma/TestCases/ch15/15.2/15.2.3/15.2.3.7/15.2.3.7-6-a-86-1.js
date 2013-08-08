/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-86-1.js
 * @description Object.defineProperties will not throw TypeError when P.configurable is false, both properties.[[Set]] and P.[[Set]] are two objects which refer to the same object and the object has been updated after defined(8.12.9 step 11.a.i)
 */


function testcase() {

        var obj = {};

        var set_func = function (value) {
            obj.setVerifyHelpProp = value;
        }

        Object.defineProperty(obj, "foo", {
            set: set_func,
            configurable: false
        });

        set_func = function (value) {
            obj.setVerifyHelpProp1 = value;
        }

        try {
            Object.defineProperties(obj, {
                foo: {
                    set: set_func
                }
            });
        } catch (e) {
            return accessorPropertyAttributesAreCorrect(obj, "foo", undefined, set_func, "setVerifyHelpProp", false, false);
        }
    }
runTestCase(testcase);
