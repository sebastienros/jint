/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-315.js
 * @description Object.create - all properties in 'Properties' are enumerable (data property and accessor property) (15.2.3.7 step 7)
 */


function testcase() {

        var newObj = {};
        function getFunc() {
            return 10;
        }
        function setFunc(value) {
            newObj.setVerifyHelpProp = value;
        }

        newObj = Object.create({}, {
            foo1: {
                value: 200,
                enumerable: true,
                writable: true,
                configurable: true
            },
            foo2: {
                get: getFunc,
                set: setFunc,
                enumerable: true,
                configurable: true
            }
        });
        return dataPropertyAttributesAreCorrect(newObj, "foo1", 200, true, true, true) &&
            accessorPropertyAttributesAreCorrect(newObj, "foo2", getFunc, setFunc, "setVerifyHelpProp", true, true);
    }
runTestCase(testcase);
