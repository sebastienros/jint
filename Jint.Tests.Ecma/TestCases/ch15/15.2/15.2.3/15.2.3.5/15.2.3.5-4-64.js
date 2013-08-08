/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-64.js
 * @description Object.create - one property in 'Properties' is the Math object that uses Object's [[Get]] method to access the 'enumerable' property (8.10.5 step 3.a)
 */


function testcase() {

        var accessed = false;

        try {
            Math.enumerable = true;

            var newObj = Object.create({}, {
                prop: Math 
            });
            for (var property in newObj) {
                if (property === "prop") {
                    accessed = true;
                }
            }
            return accessed;
        } finally {
            delete Math.enumerable;
        }
    }
runTestCase(testcase);
