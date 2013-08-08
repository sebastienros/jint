/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-85.js
 * @description Object.create - 'enumerable' property of one property in 'Properties' is an Array object (8.10.5 step 3.b)
 */


function testcase() {

        var accessed = false;

        var newObj = Object.create({}, {
            prop: {
                enumerable: []
            }
        });

        for (var property in newObj) {
            if (property === "prop") {
                accessed = true;
            }
        }
        return accessed;
    }
runTestCase(testcase);
