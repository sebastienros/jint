/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-274.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is generic own accessor property of 'O', test TypeError is thrown when updating the [[Get]] attribute value of 'P' which is defined as non-configurable (15.4.5.1 step 5)
 */


function testcase() {
        var arr = [];

        function get_fun() {
            return 37;
        }
        function set_fun(value) {
            arr.verifySetFun = value;
        }
        Object.defineProperty(arr, "property", {
            get: get_fun,
            set: set_fun
        });

        try {
            Object.defineProperties(arr, {
                "property": {
                    get: function () {
                        return 36;
                    }
                }
            });
            return false;
        } catch (ex) {
            return (ex instanceof TypeError) &&
                accessorPropertyAttributesAreCorrect(arr, "property", get_fun, set_fun, "verifySetFun", false, false);
        }
    }
runTestCase(testcase);
