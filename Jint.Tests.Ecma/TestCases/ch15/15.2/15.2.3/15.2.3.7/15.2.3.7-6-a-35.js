/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-35.js
 * @description Object.defineProperties - 'P' doesn't exist in 'O', test [[Enumerable]] of 'P' is set as false value if absent in accessor descriptor 'desc' (8.12.9 step 4.b.i)
 */


function testcase() {
        var obj = {};
        var getFun = function () {
            return 10;
        };
        var setFun = function (value) {
            obj.setVerifyHelpProp = value;
        };

        Object.defineProperties(obj, {
            prop: {
                set: setFun,
                get: getFun,
                configurable: true
            }
        });
        return accessorPropertyAttributesAreCorrect(obj, "prop", getFun, setFun, "setVerifyHelpProp", false, true);

    }
runTestCase(testcase);
