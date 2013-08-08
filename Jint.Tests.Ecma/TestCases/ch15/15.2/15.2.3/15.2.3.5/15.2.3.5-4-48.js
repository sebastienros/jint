/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-48.js
 * @description Object.create  - 'enumerable' property of one property in 'Properties' is own data property (8.10.5 step 3.a)
 */


function testcase() {

        var accessed = false;

        var newObj = Object.create({}, {
            prop: {
                enumerable: true
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
