/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-205.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index named property, 'desc' is accessor descriptor, test updating all attribute values of 'P'  (15.4.5.1 step 4.c)
 */


function testcase() {
        var arr = [];

        Object.defineProperties(arr, {
            "0": {
                get: function () {
                    return 11;
                },
                set: function () { },
                configurable: true,
                enumerable: true
            }
        });

        var setFun = function (value) {
            arr.setVerifyHelpProp = value;
        };
        var getFun = function () {
            return 14;
        };
        Object.defineProperties(arr, {
            "0": {
                get: getFun,
                set: setFun,
                configurable: false,
                enumerable: false
            }
        });

        return accessorPropertyAttributesAreCorrect(arr, "0", getFun, setFun, "setVerifyHelpProp", false, false);
    }
runTestCase(testcase);
