/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-188.js
 * @description Object.create - 'writable' property of one property in 'Properties' is own accessor property without a get function (8.10.5 step 6.a)
 */


function testcase() {

        var descObj = { value: 100 };

        Object.defineProperty(descObj, "writable", {
            set: function () { }
        });

        var newObj = Object.create({}, {
            prop: descObj
        });

        var beforeWrite = (newObj.prop === 100);

        newObj.prop = "isWritable";

        var afterWrite = (newObj.prop === 100);

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
