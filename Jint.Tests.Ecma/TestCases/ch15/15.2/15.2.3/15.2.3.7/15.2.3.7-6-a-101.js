/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-101.js
 * @description Object.defineProperties - 'P' is accessor property, both properties.[[Get]] and P.[[Get]] are two different values (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        function get_func() {
            return 10;
        }
        function set_func(value) {
            obj.setVerifyHelpProp = value;
        }

        Object.defineProperty(obj, "foo", {
            get: get_func,
            set: set_func,
            enumerable: true,
            configurable: true
        });

        function get_func2() {
            return 20;
        }

        Object.defineProperties(obj, {
            foo: {
                get: get_func2
            }
        });
        return accessorPropertyAttributesAreCorrect(obj, "foo", get_func2, set_func, "setVerifyHelpProp", true, true);
    }
runTestCase(testcase);
