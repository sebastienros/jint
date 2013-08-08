/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-3.js
 * @description Function.prototype.bind - F can get own data property that overrides an inherited data property
 */


function testcase() {

        var foo = function () { };

        var obj = foo.bind({});

        try {
            Function.prototype.property = 3;
            obj.property = 12;
            return obj.property === 12;
        } finally {
            delete Function.prototype.property;
        }
    }
runTestCase(testcase);
