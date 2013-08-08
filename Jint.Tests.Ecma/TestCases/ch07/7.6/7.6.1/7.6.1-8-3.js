/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-8-3.js
 * @description Allow reserved words as property names by set function within an object, accessed via indexing: instanceof, typeof, else
 */


function testcase() {
        var test0 = 0, test1 = 1, test2 = 2;
        var tokenCodes  = {
            set instanceof(value){
                test0 = value;
            },
            get instanceof(){
                return test0;
            },
            set typeof(value){
                test1 = value;
            },
            get typeof(){
                return test1;
            },
            set else(value){
                test2 = value;
            },
            get else(){
                return test2;
            }
        }; 
        var arr = [
            'instanceof',
            'typeof',
            'else'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
