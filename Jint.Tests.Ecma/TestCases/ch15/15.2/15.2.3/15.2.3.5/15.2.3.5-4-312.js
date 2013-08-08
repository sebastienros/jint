/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-312.js
 * @description Object.create - [[Enumerable]] is set as false if it is absent in accessor descriptor of one property in 'Properties' (8.12.9 step 4.b)
 */


function testcase() {
        var isEnumerable = false;
        var newObj = Object.create({}, {
            prop: {
                set: function () { },
                get: function () { },
                configurable: true
            }
        });
        var hasProperty = newObj.hasOwnProperty("prop");
        for (var p in newObj) {
            if (p === "prop") {
                isEnumerable = true;
            }
        }
        return hasProperty && !isEnumerable;
    }
runTestCase(testcase);
