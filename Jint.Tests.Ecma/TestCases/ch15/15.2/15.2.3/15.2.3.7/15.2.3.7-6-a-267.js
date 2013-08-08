/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-267.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is generic property, and 'desc' is accessor descriptor, test 'P' is defined in 'O' with all correct attribute values (15.4.5.1 step 5)
 */


function testcase() {

        var arr = [];

        function get_fun() {
            return 12;
        }
        function set_fun(value) {
            arr.setVerifyHelpProp = value;
        }

        Object.defineProperties(arr, {
            "property": {
                get: get_fun,
                set: set_fun,
                enumerable: true,
                configurable: true
            }
        });
        return accessorPropertyAttributesAreCorrect(arr, "property", get_fun, set_fun, "setVerifyHelpProp", true, true) &&
            arr.length === 0;
    }
runTestCase(testcase);
