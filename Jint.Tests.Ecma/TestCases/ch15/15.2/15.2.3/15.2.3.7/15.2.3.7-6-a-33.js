/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-33.js
 * @description Object.defineProperties - 'P' doesn't exist in 'O', test [[Get]] of 'P' is set as undefined value if absent in accessor descriptor 'desc' (8.12.9 step 4.b)
 */


function testcase() {
        var obj = {};
        var setFun = function (value) {
            obj.setVerifyHelpProp = value;
        };

        Object.defineProperties(obj, {
            prop: {
                set: setFun,
                enumerable: true,
                configurable: true
            }
        });
        return accessorPropertyAttributesAreCorrect(obj, "prop", undefined, setFun, "setVerifyHelpProp", true, true);

    }
runTestCase(testcase);
