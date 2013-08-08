/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-8-6.js
 * @description Allow reserved words as property names by set function within an object, accessed via indexing: continue, for, switch
 */


function testcase() {
        var test0 = 0, test1 = 1, test2 = 2;
        var tokenCodes  = {
            set continue(value){
                test0 = value;
            },
            get continue(){
                return test0;
            },
            set for(value){
                test1 = value;
            },
            get for(){
                return test1;
            },
            set switch(value){
                test2 = value;
            },
            get switch(){
                return test2;
            }
        }; 
        var arr = [
            'continue', 
            'for',
            'switch'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
