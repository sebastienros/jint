/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-11-1.js
 * @description Function.prototype.bind - internal property [[Prototype]] of 'F' is set as Function.prototype
 */


function testcase() {

        var foo = function () { };
        try {
            Function.prototype.property = 12;
            var obj = foo.bind({});

            return obj.property === 12;
        } finally {
            delete Function.prototype.property;
        }
    }
runTestCase(testcase);
