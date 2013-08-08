/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-93.js
 * @description Object.defineProperties will not throw TypeError when P.configurable is false, P.[[Get]] and properties.[[Get]] are undefined (8.12.9 step 11.a.ii)
 */


function testcase() {

        var obj = {};

        function set_func(value) {
            obj.setVerifyHelpProp = value;
        }

        Object.defineProperty(obj, "foo", {
            get: undefined,
            set: set_func,
            enumerable: false,
            configurable: false
        });

        Object.defineProperties(obj, {
            foo: {
                get: undefined
            }
        });
        return accessorPropertyAttributesAreCorrect(obj, "foo", undefined, set_func, "setVerifyHelpProp", false, false);
    }
runTestCase(testcase);
