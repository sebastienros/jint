/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-8-7.js
 * @description Allow reserved words as property names by set function within an object, accessed via indexing: while, debugger, function
 */


function testcase() {
        var test0 = 0, test1 = 1, test2 = 2;
        var tokenCodes  = {
            set while(value){
                test0 = value;
            },
            get while(){
                return test0
            },
            set debugger(value){
                test1 = value;
            },
            get debugger(){
                return test1;
            },
            set function(value){
                test2 = value;
            },
            get function(){
                return test2;
            }
        }; 
        var arr = [
            'while' ,
            'debugger', 
            'function'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
