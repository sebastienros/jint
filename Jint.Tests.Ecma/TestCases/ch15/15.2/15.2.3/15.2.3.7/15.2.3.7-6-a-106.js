/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-106.js
 * @description Object.defineProperties - 'P' is accessor property, P.[[Set]] is undefined and properties.[[Set]] is normal value (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        function get_func() {
            return 10;
        }

        Object.defineProperty(obj, "foo", {
            get: get_func,
            set: undefined,
            enumerable: true,
            configurable: true
        });

        function set_func(value) {
            obj.setVerifyHelpProp = value;
        }

        Object.defineProperties(obj, {
            foo: {
                set: set_func
            }
        });
        return accessorPropertyAttributesAreCorrect(obj, "foo", get_func, set_func, "setVerifyHelpProp", true, true);
    }
runTestCase(testcase);
