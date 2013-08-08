/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-103.js
 * @description Object.defineProperties - 'P' is accessor property, P.[[Get]] is undefined and properties.[[Get]] is normal value (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        function set_func(value) {
            obj.setVerifyHelpProp = value;
        }

        Object.defineProperty(obj, "foo", {
            get: undefined,
            set: set_func,
            enumerable: true,
            configurable: true
        });

        function get_func() {
            return 10;
        }

        Object.defineProperties(obj, {
            foo: {
                get: get_func
            }
        });
        return accessorPropertyAttributesAreCorrect(obj, "foo", get_func, set_func, "setVerifyHelpProp", true, true);
    }
runTestCase(testcase);
